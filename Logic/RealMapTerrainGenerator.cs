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
	private const int FIXED_RESOLUTION = 50; // Фиксированное разрешение 50x50
	private const int MAX_POINTS_PER_REQUEST = 100; // Лимит API OpenTopoData
	private const int MAX_REQUESTS = 25; // Максимум запросов (50x50 = 2500 точек / 100 = 25 запросов)
	private const int REQUEST_DELAY_MS = 1000; // Задержка после успешного запроса
	private const int RETRY_DELAY_MS = 1000; // Задержка при повторной попытке
	private const int MAX_RETRIES = 5; // Количество повторных попыток при ошибке
	private const int REQUEST_TIMEOUT_SECONDS = 1; // Таймаут запроса

	// Диапазон нормализованных высот (в метрах)
	private const float TARGET_MIN_HEIGHT = 0f;
	private const float TARGET_MAX_HEIGHT = 200f;

	// Параметры масштабирования меша
	private const float MAX_MESH_UNITS = 200f;
	private const float MIN_MESH_UNITS = 8f;
	private const float METERS_TO_UNITS = 0.01f;
	private const float VERTICAL_SCALE = 1.0f; // Будет переопределен динамически
	private const float HEIGHT_TO_MESH_RATIO = 0.15f; // Высоты занимают 15% от размера меша

	// Режимы разрешения
	public enum ResolutionMode
	{
		HighQuality = 0,    // 50x50 (25 запросов)
		MediumQuality = 1,  // 31x31 (10 запросов)
		Adaptive = 2        // Адаптивное
	}

	// Делегат для обновления прогресса
	public delegate void ProgressCallback(float progress, string status);
	
	// Основной метод генерации рельефа
	public static async Task<Node3D> Generate(
		Node3D parent,
		float leftUpLat,
		float leftUpLng,
		float rightDownLat,
		float rightDownLng,
		Node owner,
		int resolutionMode = 0,
		ProgressCallback progressCallback = null
	)
	{
		// Логирование границ
		GD.Print("=== Генерация реального рельефа OpenTopoData ===");
		GD.Print($"Input bounds raw: NW({leftUpLat.ToString(CultureInfo.InvariantCulture)},{leftUpLng.ToString(CultureInfo.InvariantCulture)}) SE({rightDownLat.ToString(CultureInfo.InvariantCulture)},{rightDownLng.ToString(CultureInfo.InvariantCulture)})");

		progressCallback?.Invoke(5.0f, "Загрузка высотных данных...");
		
		// Загружаем матрицу высот
		float[,] heights = await RequestHeights(leftUpLat, leftUpLng, rightDownLat, rightDownLng, resolutionMode, progressCallback);

		// Проверка на ошибки
		if (heights == null)
		{
			GD.PrintErr("❌ heights = null");
			return null;
		}

		// Вывод статистики
		PrintStats("После загрузки", heights);

		progressCallback?.Invoke(70.0f, "Обработка данных...");
		
		// Заполняем отсутствующие значения
		FillMissingHeights(heights);

		progressCallback?.Invoke(75.0f, "Построение меша...");
		
		// Строим меш на основе высот и получаем размер меша
		float meshSizeUnits;
		Mesh mesh = BuildCenteredMesh(heights, leftUpLat, leftUpLng, rightDownLat, rightDownLng, out meshSizeUnits);

		// Проверка валидности меша
		if (mesh == null || mesh.GetSurfaceCount() == 0)
		{
			GD.PrintErr("❌ mesh пуст");
			return null;
		}

		progressCallback?.Invoke(80.0f, "Создание экземпляра меша...");
		
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

		// Получаем размеры сетки из размеров массива высот
		int meshResX = heights.GetLength(0);
		int meshResZ = heights.GetLength(1);
		
		GD.Print($"🎨 Applying textures: mesh resolution {meshResX}x{meshResZ}");

		progressCallback?.Invoke(85.0f, "Применение текстур...");

		// Накладываем текстуры по высоте используя специальный класс для реального мира
		// Передаем исходный массив высот напрямую, чтобы избежать проблем с порядком вершин
		RealWorldTexturePainter.ApplyHeightTexture(
			meshInstance,
			heights,
			meshResX,
			meshResZ,
			"res://textures/sand.png",
			"res://textures/grass.png",
			"res://textures/rock.png",
			0.35f,
			0.65f
		);

		GD.Print("✅ Текстуры применены");
		
		progressCallback?.Invoke(100.0f, "Генерация завершена!");


		return meshInstance;
	}

	// Метод делает запрос к API и возвращает 2D массив высот
	private static async Task<float[,]> RequestHeights(
		float leftUpLat,
		float leftUpLng,
		float rightDownLat,
		float rightDownLng,
		int resolutionMode = 0,
		ProgressCallback progressCallback = null
	)
	{
		// Нормализуем координаты в N/S/W/E
		float north = Mathf.Max(leftUpLat, rightDownLat);
		float south = Mathf.Min(leftUpLat, rightDownLat);
		float west = Mathf.Min(leftUpLng, rightDownLng);
		float east = Mathf.Max(leftUpLng, rightDownLng);

		// Выводим нормализованные границы
		GD.Print($"Normalized bounds: N={north.ToString(CultureInfo.InvariantCulture)}, S={south.ToString(CultureInfo.InvariantCulture)}, W={west.ToString(CultureInfo.InvariantCulture)}, E={east.ToString(CultureInfo.InvariantCulture)}");

		// Вычисляем разницу по широте/долготе
		float dLat = Math.Abs(north - south);
		float dLon = Math.Abs(east - west);

		// Определяем разрешение в зависимости от выбранного режима
		int resolution;
		ResolutionMode mode = (ResolutionMode)resolutionMode;
		
		switch (mode)
		{
			case ResolutionMode.HighQuality:
				resolution = 50; // 50x50 = 2500 точек = 25 запросов
				GD.Print($"🌍 Режим: Высокое качество (50x50, ~25 запросов, дольше по времени)");
				break;
			case ResolutionMode.MediumQuality:
				resolution = 31; // 31x31 = 961 точка = 10 запросов
				GD.Print($"🌍 Режим: Среднее качество (31x31, ~10 запросов, быстрее)");
				break;
			case ResolutionMode.Adaptive:
			default:
				// Адаптивное разрешение в зависимости от размера области
				if (dLat < 0.01f && dLon < 0.01f)
					resolution = 50; // Маленькая область - высокое разрешение
				else if (dLat < 0.05f && dLon < 0.05f)
					resolution = 40; // Средняя область
				else if (dLat < 0.2f && dLon < 0.2f)
					resolution = 31; // Большая область - среднее разрешение
				else
					resolution = 25; // Очень большая область - низкое разрешение
				GD.Print($"🌍 Режим: Адаптивное (разрешение {resolution}x{resolution} в зависимости от размера области)");
				break;
		}

		GD.Print($"🌍 Resolution: {resolution}x{resolution} (dLat={dLat:F6}, dLon={dLon:F6})");

		// Массив высот (будет пересоздан, если разрешение изменится)
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
		http.Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS);
		http.DefaultRequestHeaders.Add("User-Agent", "GodotTerrainPlugin/1.0");

		int idx = 0;
		int reqCount = 0;

		// API OpenTopoData ограничивает количество точек в одном запросе до 100
		// Вычисляем размер батча и количество запросов
		int totalPoints = points.Count;
		int batchSize = MAX_POINTS_PER_REQUEST;
		
		// Вычисляем необходимое количество запросов
		int requiredRequests = (int)Mathf.Ceil(totalPoints / (float)batchSize);
		
		// Проверяем, не превышаем ли лимит запросов
		if (requiredRequests > MAX_REQUESTS)
		{
			GD.PrintErr($"⚠️ ВНИМАНИЕ: Требуется {requiredRequests} запросов, но максимум {MAX_REQUESTS}!");
			GD.PrintErr($"⚠️ Это означает, что разрешение {resolution}x{resolution} слишком большое.");
			GD.PrintErr($"⚠️ Уменьшаем разрешение до {Mathf.Sqrt(MAX_REQUESTS * batchSize):F0}x{Mathf.Sqrt(MAX_REQUESTS * batchSize):F0}");
			
			// Пересчитываем с меньшим разрешением
			int newResolution = (int)Mathf.Sqrt(MAX_REQUESTS * batchSize);
			// Округляем вниз
			newResolution = (newResolution / 2) * 2;
			
			// Пересоздаем точки с новым разрешением
			points.Clear();
			for (int z = 0; z < newResolution; z++)
			{
				float lat = Mathf.Lerp(north, south, (float)z / (newResolution - 1));
				for (int x = 0; x < newResolution; x++)
				{
					float lng = Mathf.Lerp(west, east, (float)x / (newResolution - 1));
					points.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1}", lat, lng));
				}
			}
			
			// Пересоздаем массив данных
			data = new float[newResolution, newResolution];
			resolution = newResolution;
			totalPoints = points.Count;
			requiredRequests = (int)Mathf.Ceil(totalPoints / (float)batchSize);
			
			GD.Print($"✅ Новое разрешение: {resolution}x{resolution} ({totalPoints} точек, {requiredRequests} запросов)");
		}
		
		int maxAllowedRequests = Mathf.Min(MAX_REQUESTS, requiredRequests);
		
		GD.Print($"📦 Batch size: {batchSize} points per request (total: {totalPoints} points, {requiredRequests} requests, max {maxAllowedRequests} allowed)");
		
		while (idx < points.Count && reqCount < maxAllowedRequests)
		{
			int take = Mathf.Min(batchSize, points.Count - idx);
			var batch = points.GetRange(idx, take);

			string url = API_URL + string.Join("|", batch);

			reqCount++;
			
			// Обновляем прогресс
			float progress = 5.0f + (reqCount / (float)maxAllowedRequests) * 65.0f; // От 5% до 70%
			progressCallback?.Invoke(progress, $"Запрос {reqCount}/{maxAllowedRequests} к API...");

			// Логируем параметры запроса
			GD.Print($"📡 Запрос {reqCount}/{maxAllowedRequests} ({take} точек, осталось {points.Count - idx} точек)");
			GD.Print($"🔹 URL (начало): {url.Substring(0, Mathf.Min(200, url.Length))}");
			GD.Print($"🔹 Примеры точек: first={batch[0]} last={batch[batch.Count - 1]}");

			// Пытаемся выполнить запрос с повторными попытками
			bool success = false;
			int retryCount = 0;
			System.Net.Http.HttpResponseMessage resp = null;

			while (!success && retryCount < MAX_RETRIES)
			{
				try
				{
					// Выполняем GET запрос
					resp = await http.GetAsync(url);
					
					// Проверка статуса
					if (resp.IsSuccessStatusCode)
					{
						success = true;
					}
					else
					{
						GD.PrintErr($"❌ HTTP {resp.StatusCode} on request #{reqCount}, attempt {retryCount + 1}/{MAX_RETRIES}");
						retryCount++;
						if (retryCount < MAX_RETRIES)
						{
							await Task.Delay(RETRY_DELAY_MS * (retryCount + 1)); // Увеличиваем задержку с каждой попыткой
						}
					}
				}
				catch (System.Net.Http.HttpRequestException ex)
				{
					// Ошибка сети
					GD.PrintErr($"❌ Network error on request #{reqCount}, attempt {retryCount + 1}/{MAX_RETRIES}: {ex.Message}");
					retryCount++;
					if (retryCount < MAX_RETRIES)
					{
						await Task.Delay(RETRY_DELAY_MS * (retryCount + 1)); // Увеличиваем задержку с каждой попыткой
					}
				}
				catch (TaskCanceledException ex)
				{
					// Таймаут
					GD.PrintErr($"⏱️ Timeout on request #{reqCount}, attempt {retryCount + 1}/{MAX_RETRIES}: {ex.Message}");
					retryCount++;
					if (retryCount < MAX_RETRIES)
					{
						GD.Print($"🔄 Retrying request #{reqCount} in {RETRY_DELAY_MS * (retryCount + 1) / 1000.0} seconds...");
						await Task.Delay(RETRY_DELAY_MS * (retryCount + 1)); // Увеличиваем задержку с каждой попыткой
					}
				}
				catch (Exception ex)
				{
					// Другие ошибки
					GD.PrintErr($"❌ Unexpected error on request #{reqCount}, attempt {retryCount + 1}/{MAX_RETRIES}: {ex.Message}");
					retryCount++;
					if (retryCount < MAX_RETRIES)
					{
						await Task.Delay(RETRY_DELAY_MS * (retryCount + 1));
					}
				}
			}

			// Если все попытки неудачны, пропускаем этот батч
			if (!success || resp == null)
			{
				GD.PrintErr($"❌ Failed to load batch {reqCount} after {MAX_RETRIES} attempts. Skipping {take} points.");
				// Заполняем пропущенные точки NaN
				for (int i = 0; i < take; i++)
				{
					int flat = idx + i;
					if (flat < resolution * resolution)
					{
						int x = flat % resolution;
						int z = flat / resolution;
						data[x, z] = float.NaN;
					}
				}
				idx += take;
				await Task.Delay(RETRY_DELAY_MS);
				continue;
			}

			// Успешный запрос - обрабатываем ответ
			if (retryCount > 0)
			{
				GD.Print($"✅ Request #{reqCount} succeeded after {retryCount + 1} attempts");
			}
			else
			{
				GD.Print($"✅ Request #{reqCount} succeeded");
			}

			// Чтение JSON
			string json = await resp.Content.ReadAsStringAsync();
			GD.Print($"🔹 RAW JSON (начало): {json.Substring(0, Mathf.Min(400, json.Length))}");

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

			// Пауза после успешного запроса перед следующим
			if (idx < points.Count && reqCount < maxAllowedRequests)
			{
				GD.Print($"⏸️ Pausing {REQUEST_DELAY_MS / 1000.0} seconds before next request...");
				await Task.Delay(REQUEST_DELAY_MS);
			}
		}

		// Проверка на неполную загрузку данных
		int loadedCount = 0;
		int nanCount = 0;
		for (int z = 0; z < resolution; z++)
		{
			for (int x = 0; x < resolution; x++)
			{
				if (!float.IsNaN(data[x, z]))
					loadedCount++;
				else
					nanCount++;
			}
		}

		GD.Print($"📊 Загружено точек: {loadedCount}/{resolution * resolution}, NaN: {nanCount}");
		
		if (idx < points.Count)
		{
			GD.PrintErr($"⚠️ ВНИМАНИЕ: Загружено не все данные! Осталось {points.Count - idx} точек из {points.Count}");
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
	private static Mesh BuildCenteredMesh(float[,] heights, float leftUpLat, float leftUpLng, float rightDownLat, float rightDownLng, out float sizeUnits)
	{
		int resX = heights.GetLength(0);
		int resZ = heights.GetLength(1);

		// Средняя широта в радианах
		float meanLat = (leftUpLat + rightDownLat) * 0.5f;
		meanLat = Mathf.DegToRad(meanLat);
		const float METERS_PER_DEGREE_LAT = 111320f;
		float metersPerDegLon = Mathf.Cos(meanLat) * METERS_PER_DEGREE_LAT;

		// Нормализуем координаты для правильного расчета размеров
		float north = Mathf.Max(leftUpLat, rightDownLat);
		float south = Mathf.Min(leftUpLat, rightDownLat);
		float west = Mathf.Min(leftUpLng, rightDownLng);
		float east = Mathf.Max(leftUpLng, rightDownLng);

		// Вычисляем реальные размеры в метрах
		float widthMeters = Math.Abs(east - west) * metersPerDegLon;
		float depthMeters = Math.Abs(north - south) * METERS_PER_DEGREE_LAT;

		// Вычисляем итоговый размер меша в юнитах Godot
		float desiredSize = Mathf.Max(widthMeters, depthMeters);
		sizeUnits = desiredSize * METERS_TO_UNITS;
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

		// Вычисляем масштаб для высот: масштабируем пропорционально размеру меша
		// Это гарантирует, что рельеф будет заметен независимо от размера меша
		float heightRange = maxH - minH;
		
		// Вычисляем масштаб так, чтобы максимальная высота занимала HEIGHT_TO_MESH_RATIO от размера меша
		float targetMaxHeight = sizeUnits * HEIGHT_TO_MESH_RATIO;
		float heightScale = heightRange > 0.001f ? targetMaxHeight / heightRange : 1f;
		
		GD.Print($"📏 Heights: min={minH:F1}m, max={maxH:F1}m, range={heightRange:F1}m");
		GD.Print($"📏 Mesh size: {sizeUnits:F2} units, target max height: {targetMaxHeight:F2} units");
		GD.Print($"📏 Height scale: {heightScale:F6} (height range {heightRange:F1}m -> {targetMaxHeight:F2} units)");
		
		// Проверяем, есть ли вариация в высотах
		if (heightRange < 1.0f)
		{
			GD.PrintErr($"⚠️ ВНИМАНИЕ: Очень маленький диапазон высот ({heightRange:F2}m)! Ландшафт будет плоским.");
		}

		// Генерируем вершины и UV
		for (int z = 0; z < resZ; z++)
		{
			for (int x = 0; x < resX; x++)
			{
				float vx = x * stepX - halfX;
				float vz = z * stepZ - halfZ;
				
				// Высоты в метрах преобразуем в юниты Godot
				float height = heights[x, z];
				if (float.IsNaN(height))
				{
					height = minH; // Используем минимальную высоту для NaN
				}
				
				// Преобразуем высоту из метров в юниты Godot
				// Вычитаем minH чтобы начать с нуля, затем масштабируем пропорционально размеру меша
				float heightInMeters = height - minH; // Относительная высота от минимума
				float vy = heightInMeters * heightScale;
				
				// Логируем для отладки (только первые несколько вершин)
				if (x < 3 && z < 3)
				{
					GD.Print($"Vertex [{x},{z}]: height={height:F1}m, relative={heightInMeters:F1}m, vy={vy:F3} units");
				}

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

		// Возвращаем меш и размер
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


}
