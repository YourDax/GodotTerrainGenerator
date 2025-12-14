using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

// Класс для генерации реального рельефа по данным API OpenTopoData
public static class RealMapTerrainGenerator
{
	// URL API для запроса высот
	private const string API_URL = "https://api.opentopodata.org/v1/srtm90m?locations=";
	// Процент высоты воды относительно диапазона высот
	private const float WATER_PERCENT = 0.15f;

	// Параметры пакетной отправки запросов
	private const int BATCH_SIZE = 100;
	private const int MAX_REQUESTS = 10;
	private const int REQUEST_DELAY_MS = 7000;

	// Диапазон нормализованных высот (в метрах)
	private const float TARGET_MIN_HEIGHT = 0f;
	private const float TARGET_MAX_HEIGHT = 200f;

	// Параметры масштабирования меша
	private const float MAX_MESH_UNITS = 200f;
	private const float MIN_MESH_UNITS = 8f;
	private const float METERS_TO_UNITS = 0.01f;
	private const float VERTICAL_SCALE = 0.5f;

	// Основной метод генерации рельефа
	public static async Task<Node3D> Generate(
		Node3D parent,
		float leftUpLat,
		float leftUpLng,
		float rightDownLat,
		float rightDownLng,
		Node owner
	)
	{
		// Логирование границ
		GD.Print("=== Генерация реального рельефа OpenTopoData ===");
		GD.Print($"Input bounds raw: NW({leftUpLat.ToString(CultureInfo.InvariantCulture)},{leftUpLng.ToString(CultureInfo.InvariantCulture)}) SE({rightDownLat.ToString(CultureInfo.InvariantCulture)},{rightDownLng.ToString(CultureInfo.InvariantCulture)})");

		// Загружаем матрицу высот
		float[,] heights = await RequestHeights(leftUpLat, leftUpLng, rightDownLat, rightDownLng);

		// Проверка на ошибки
		if (heights == null)
		{
			GD.PrintErr("❌ heights = null");
			return null;
		}

		// Вывод статистики
		PrintStats("После загрузки", heights);

		// Заполняем отсутствующие значения
		FillMissingHeights(heights);

		// Строим меш на основе высот
		Mesh mesh = BuildCenteredMesh(heights, leftUpLat, leftUpLng, rightDownLat, rightDownLng);

		// Проверка валидности меша
		if (mesh == null || mesh.GetSurfaceCount() == 0)
		{
			GD.PrintErr("❌ mesh пуст");
			return null;
		}

		// Создаём MeshInstance3D
		var meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			Name = "GeneratedTerrain"
		};

		// Поворачиваем меш, чтобы высоты шли вверх
		meshInstance.RotateX(Mathf.Pi);

		// Добавляем в сцену
		parent.AddChild(meshInstance);
		if (owner != null) meshInstance.Owner = owner;

		GD.Print("✅ MeshInstance добавлен в сцену");

		// Получаем min/max высот
		GetMinMax(heights, out float minH, out float maxH);

		// Накладываем текстуры по высоте
		TerrainTexturePainter.ApplyHeightTexture(
			meshInstance,
			minH,
			maxH,
			"res://textures/sand.png",
			"res://textures/grass.png",
			"res://textures/rock.png",
			null,
			0.35f,
			0.55f
		);

		GD.Print("✅ Текстуры применены");

		// Генерируем воду
		GenerateWater(parent, owner, minH, maxH);

		return meshInstance;
	}

	// Метод делает запрос к API и возвращает 2D массив высот
	private static async Task<float[,]> RequestHeights(
		float leftUpLat,
		float leftUpLng,
		float rightDownLat,
		float rightDownLng
	)
	{
		// Нормализуем координаты в N/S/W/E
		float north = Math.Max(leftUpLat, rightDownLat);
		float south = Math.Min(leftUpLat, rightDownLat);
		float west = Math.Min(leftUpLng, rightDownLng);
		float east = Math.Max(leftUpLng, rightDownLng);

		// Выводим нормализованные границы
		GD.Print($"Normalized bounds: N={north.ToString(CultureInfo.InvariantCulture)}, S={south.ToString(CultureInfo.InvariantCulture)}, W={west.ToString(CultureInfo.InvariantCulture)}, E={east.ToString(CultureInfo.InvariantCulture)}");

		// Вычисляем разницу по широте/долготе
		float dLat = Math.Abs(north - south);
		float dLon = Math.Abs(east - west);

		// Подбираем разрешение сетки в зависимости от площади
		int resolution =
			(dLat < 0.01f && dLon < 0.01f) ? 32 :
			(dLat < 0.05f && dLon < 0.05f) ? 24 :
			(dLat < 0.2f && dLon < 0.2f) ? 16 : 10;

		GD.Print($"🌍 Adaptive resolution: {resolution}x{resolution} (dLat={dLat:F6}, dLon={dLon:F6})");

		// Массив высот
		float[,] data = new float[resolution, resolution];

		// Формируем список координат
		var points = new List<string>(resolution * resolution);
		for (int z = 0; z < resolution; z++)
		{
			float lat = Mathf.Lerp(north, south, (float)z / (resolution - 1));
			for (int x = 0; x < resolution; x++)
			{
				float lng = Mathf.Lerp(west, east, (float)x / (resolution - 1));
				points.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1}", lat, lng));
			}
		}

		// Лог количества точек
		GD.Print($"Сгенерировано точек: {points.Count}");

		// HttpClient для запроса
		using var http = new System.Net.Http.HttpClient();
		http.Timeout = TimeSpan.FromSeconds(30);
		http.DefaultRequestHeaders.Add("User-Agent", "GodotTerrainPlugin/1.0");

		int idx = 0;
		int reqCount = 0;

		// Отправляем запросы пакетами
		while (idx < points.Count && reqCount < MAX_REQUESTS)
		{
			int take = Math.Min(BATCH_SIZE, points.Count - idx);
			var batch = points.GetRange(idx, take);

			string url = API_URL + string.Join("|", batch);

			reqCount++;

			// Логируем параметры запроса
			GD.Print($"📡 Запрос {reqCount}/{Math.Ceiling(points.Count / (float)BATCH_SIZE)} ({take} точек)");
			GD.Print($"🔹 URL (начало): {url.Substring(0, Math.Min(200, url.Length))}");
			GD.Print($"🔹 Примеры точек: first={batch[0]} last={batch[batch.Count - 1]}");

			System.Net.Http.HttpResponseMessage resp;

			try
			{
				// Выполняем GET запрос
				resp = await http.GetAsync(url);
			}
			catch (Exception ex)
			{
				// Ошибка сети
				GD.PrintErr($"❌ Network error on request #{reqCount}: {ex.Message}");
				idx += take;
				await Task.Delay(REQUEST_DELAY_MS);
				continue;
			}

			// Проверка статуса
			if (!resp.IsSuccessStatusCode)
			{
				GD.PrintErr($"❌ HTTP {resp.StatusCode} on request #{reqCount}");
				idx += take;
				await Task.Delay(REQUEST_DELAY_MS);
				continue;
			}

			// Чтение JSON
			string json = await resp.Content.ReadAsStringAsync();
			GD.Print($"🔹 RAW JSON (начало): {json.Substring(0, Math.Min(400, json.Length))}");

			Godot.Collections.Dictionary parsed;
			try
			{
				// Парсим JSON в словарь Godot
				parsed = Godot.Json.ParseString(json).AsGodotDictionary();
			}
			catch (Exception ex)
			{
				GD.PrintErr($"❌ JSON parse error on request #{reqCount}: {ex.Message}");
				idx += take;
				await Task.Delay(REQUEST_DELAY_MS);
				continue;
			}

			// Проверка наличия поля results
			if (!parsed.ContainsKey("results"))
			{
				GD.PrintErr($"❌ Ответ не содержит 'results' (req #{reqCount})");
				idx += take;
				await Task.Delay(REQUEST_DELAY_MS);
				continue;
			}

			var results = parsed["results"].AsGodotArray();

			// Читаем elevation из каждого результата
			for (int i = 0; i < results.Count; i++)
			{
				int flat = idx + i;
				if (flat >= resolution * resolution) break;

				var r = results[i].AsGodotDictionary();

				float elev;

				if (!r.ContainsKey("elevation") || r["elevation"].VariantType == Variant.Type.Nil)
				{
					// elevation отсутствует
					elev = float.NaN;
					GD.PrintErr($"⚠ elevation NULL at flatIndex={flat}");
				}
				else
				{
					// Парсим значение elevation
					Variant v = r["elevation"];

					if (v.VariantType == Variant.Type.Float)
						elev = v.AsSingle();
					else if (v.VariantType == Variant.Type.Int)
						elev = v.AsInt32();
					else if (v.VariantType == Variant.Type.String)
					{
						float.TryParse(v.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out elev);
					}
					else elev = float.NaN;
				}

				// Преобразуем flat-index в координаты
				int x = flat % resolution;
				int z = flat / resolution;

				data[x, z] = elev;
			}

			// Продвигаем индекс
			idx += take;

			await Task.Delay(REQUEST_DELAY_MS);
		}

		GD.Print("✅ Высотные данные загружены.");
		return data;
	}

	// Заполнение пропущенных значений высот
	private static void FillMissingHeights(float[,] data)
	{
		int resX = data.GetLength(0);
		int resZ = data.GetLength(1);

		// Один проход интерполяции соседями
		for (int z = 0; z < resZ; z++)
		{
			for (int x = 0; x < resX; x++)
			{
				if (!float.IsNaN(data[x, z])) continue;

				float sum = 0;
				int count = 0;

				// Локальный метод добавления соседей
				void TryAdd(int xx, int zz)
				{
					if (xx >= 0 && xx < resX && zz >= 0 && zz < resZ && !float.IsNaN(data[xx, zz]))
					{
						sum += data[xx, zz];
						count++;
					}
				}

				// Смотрим вокруг клетки
				TryAdd(x - 1, z);
				TryAdd(x + 1, z);
				TryAdd(x, z - 1);
				TryAdd(x, z + 1);
				TryAdd(x - 1, z - 1);
				TryAdd(x + 1, z - 1);
				TryAdd(x - 1, z + 1);
				TryAdd(x + 1, z + 1);

				// Если есть валидные соседи — усредняем
				if (count > 0)
				{
					data[x, z] = sum / count;
				}
			}
		}

		// Оставшиеся NaN заменяем на 0
		for (int z = 0; z < resZ; z++)
			for (int x = 0; x < resX; x++)
				if (float.IsNaN(data[x, z])) data[x, z] = 0f;
	}

	// Построение меша на основе матрицы высот
	private static Mesh BuildCenteredMesh(float[,] heights, float leftUpLat, float leftUpLng, float rightDownLat, float rightDownLng)
	{
		int resX = heights.GetLength(0);
		int resZ = heights.GetLength(1);

		// Средняя широта в радианах
		float meanLat = (leftUpLat + rightDownLat) * 0.5f * Mathf.DegToRad;
		const float METERS_PER_DEGREE_LAT = 111320f;
		float metersPerDegLon = Mathf.Cos(meanLat) * METERS_PER_DEGREE_LAT;

		// Вычисляем реальные размеры в метрах
		float widthMeters = Math.Abs(rightDownLng - leftUpLng) * metersPerDegLon;
		float depthMeters = Math.Abs(rightDownLat - leftUpLat) * METERS_PER_DEGREE_LAT;

		// Вычисляем итоговый размер меша в юнитах Godot
		float desiredSize = Math.Max(widthMeters, depthMeters);
		float sizeUnits = desiredSize * METERS_TO_UNITS;
		sizeUnits = Mathf.Clamp(sizeUnits, MIN_MESH_UNITS, MAX_MESH_UNITS);

		if (float.IsNaN(sizeUnits) || sizeUnits <= 0f) sizeUnits = MIN_MESH_UNITS;

		// Шаг между вершинами
		float stepX = sizeUnits / (resX - 1);
		float stepZ = sizeUnits / (resZ - 1);

		// Масштабирование под максимум
		float targetUnits = MAX_MESH_UNITS;
		float scaleX = targetUnits / sizeUnits;
		float scaleZ = targetUnits / sizeUnits;
		float finalScale = Mathf.Min(Mathf.Min(scaleX, scaleZ), 1f);

		stepX *= finalScale;
		stepZ *= finalScale;
		sizeUnits *= finalScale;

		// Логируем масштабирование
		GD.Print($"✅ Mesh scaled: final size={sizeUnits:F2} units");
		GD.Print($"Real size (m): width={widthMeters:F1}, depth={depthMeters:F1} -> mesh units size={sizeUnits:F2}");

		// Создаём SurfaceTool
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		// Массивы вершин и UV
		Vector3[] verts = new Vector3[resX * resZ];
		Vector2[] uvs = new Vector2[resX * resZ];

		float halfX = sizeUnits * 0.5f;
		float halfZ = sizeUnits * 0.5f;

		// Получаем min/max высот
		GetMinMax(heights, out float minH, out float maxH);

		// Генерируем вершины и UV
		for (int z = 0; z < resZ; z++)
		{
			for (int x = 0; x < resX; x++)
			{
				float vx = x * stepX - halfX;
				float vz = z * stepZ - halfZ;
				float vy = heights[x, z] * VERTICAL_SCALE;

				int idx = z * resX + x;
				verts[idx] = new Vector3(vx, vy, vz);
				uvs[idx] = new Vector2((float)x / (resX - 1), (float)z / (resZ - 1));
			}
		}

		// Создаём треугольники (индексы)
		for (int z = 0; z < resZ - 1; z++)
		{
			for (int x = 0; x < resX - 1; x++)
			{
				int i0 = z * resX + x;
				int i1 = i0 + 1;
				int i2 = i0 + resX;
				int i3 = i2 + 1;

				st.SetUV(uvs[i0]); st.AddVertex(verts[i0]);
				st.SetUV(uvs[i2]); st.AddVertex(verts[i2]);
				st.SetUV(uvs[i1]); st.AddVertex(verts[i1]);

				st.SetUV(uvs[i1]); st.AddVertex(verts[i1]);
				st.SetUV(uvs[i2]); st.AddVertex(verts[i2]);
				st.SetUV(uvs[i3]); st.AddVertex(verts[i3]);
			}
		}

		// Генерируем нормали
		st.GenerateNormals();

		return st.Commit();
	}

	// Нормализация значений в диапазон
	private static void NormalizeToRange(float[,] h, float minTarget, float maxTarget)
	{
		GetMinMax(h, out float min, out float max);

		if (Math.Abs(max - min) < 0.001f)
		{
			// Почти нет рельефа — генерируем простой наклон
			for (int x = 0; x < h.GetLength(0); x++)
				for (int z = 0; z < h.GetLength(1); z++)
					h[x, z] = (x + z) * 0.1f;
			return;
		}

		// Линейная нормализация
		for (int x = 0; x < h.GetLength(0); x++)
			for (int z = 0; z < h.GetLength(1); z++)
			{
				float t = Mathf.InverseLerp(min, max, h[x, z]);
				h[x, z] = Mathf.Lerp(minTarget, maxTarget, t);
			}
	}

	// Получение min/max
	private static void GetMinMax(float[,] h, out float min, out float max)
	{
		min = float.MaxValue;
		max = float.MinValue;
		bool any = false;

		for (int z = 0; z < h.GetLength(1); z++)
		{
			for (int x = 0; x < h.GetLength(0); x++)
			{
				float v = h[x, z];
				if (float.IsNaN(v)) continue;
				any = true;
				if (v < min) min = v;
				if (v > max) max = v;
			}
		}

		if (!any)
		{
			min = 0f;
			max = 0f;
		}
	}

	// Логирование статистики
	private static void PrintStats(string label, float[,] h)
	{
		GetMinMax(h, out float min, out float max);
		GD.Print($"[{label}] min={min} max={max} delta={max - min}");
	}

	// Генерация плоскости воды
	private static void GenerateWater(Node3D parent, Node owner, float minH, float maxH)
	{
		var random = new RandomTerrainGenerator();

		float worldWater = Mathf.Lerp(minH, maxH, WATER_PERCENT);

		var water = random.GenerateWaterPlane(
			200,
			200,
			worldWater
		);

		parent.AddChild(water);
		if (owner != null) water.Owner = owner;
	}
}
