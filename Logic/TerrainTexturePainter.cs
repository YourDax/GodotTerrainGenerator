using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Tool]
public static class TerrainTexturePainter
{
	private static readonly bool VerboseTextureLogs = false;
	private static readonly Dictionary<string, Image> ImageCache = new();

	public static async Task ApplyHeightTexture(
		// Меш, на который накладываем текстуру
		MeshInstance3D meshInstance,
		// Минимальная высота меша
		float minHeight,
		// Максимальная высота меша
		float maxHeight,
		// Путь к текстуре песка
		string sandPath,
		// Путь к текстуре травы
		string grassPath,
		// Путь к текстуре камня
		string rockPath,
		// Путь для сохранения итоговой текстуры (опционально)
		string savePath = null,
		// Граница песок-трава
		float sandGrass = 0.35f,
		// Граница трава-камень
		float grassRock = 0.55f,
		// Размер карты по X (для расчета разрешения текстуры)
		int mapSizeX = 50,
		// Размер карты по Z (для расчета разрешения текстуры)
		int mapSizeZ = 50,
		// Режим генерации текстур: 0 = по высоте, 1 = камень на склонах
		int textureMode = 0,
		// Плавность перехода на склонах (0.0 = резкий, 1.0 = плавный)
		float slopeBlend = 0.5f,
		// Маска дорог (2D массив float от 0 до 1, где 1 означает наличие дороги)
		float[,] roadMask = null,
		// Путь к текстуре дороги
		string roadPath = null,
		Action<float, string> progressCallback = null,
		Func<bool> cancelRequested = null
	)
	{
		if (cancelRequested != null && cancelRequested())
			return;
		progressCallback?.Invoke(52.0f, "Подготовка текстур...");
		// Проверяем, есть ли у MeshInstance3D Mesh
		if (meshInstance.Mesh == null)
		{
			GD.PrintErr("MeshInstance3D не содержит Mesh!");
			return;
		}

		// Пробуем привести Mesh к ArrayMesh
		ArrayMesh arrayMesh = meshInstance.Mesh as ArrayMesh;
		if (arrayMesh == null)
		{
			GD.PrintErr("Mesh не является ArrayMesh!");
			return;
		}

		// Проверка путей к текстурам
		if (string.IsNullOrEmpty(sandPath) || string.IsNullOrEmpty(grassPath) || string.IsNullOrEmpty(rockPath))
		{
			GD.PrintErr("Пути к текстурам не указаны!");
			return;
		}

		Image sandImg = LoadImageCached(sandPath, "песка");
		if (sandImg == null) return;
		Image grassImg = LoadImageCached(grassPath, "травы");
		if (grassImg == null) return;
		Image rockImg = LoadImageCached(rockPath, "камня");
		if (rockImg == null) return;
		
		// Загружаем текстуру дороги, если указана маска дорог
		// ВАЖНО: Загружаем текстуру даже если roadPath пустой - используем путь по умолчанию
		Image roadImg = null;
		if (roadMask != null)
		{
			roadImg = new Image();
			bool loaded = false;
			
			// Список возможных путей для текстуры дороги
			var possibleRoadPaths = new System.Collections.Generic.List<string>();
			
			// Если путь указан пользователем, добавляем его первым
			if (!string.IsNullOrEmpty(roadPath))
			{
				possibleRoadPaths.Add(roadPath);
			}
			
			// Добавляем пути по умолчанию (в приоритетном порядке)
			// Основной путь по умолчанию - res://addons/terragenerating/Texture/road.jpg
			possibleRoadPaths.Add(TerraConfig.DefaultRoadTexturePath);
			possibleRoadPaths.Add($"{TerraConfig.AddonRootPath}/Texture/road.png");
			possibleRoadPaths.Add("res://Texture/road.jpg");
			possibleRoadPaths.Add("res://Texture/road.png");
			possibleRoadPaths.Add("res://textures/road.jpg");
			possibleRoadPaths.Add("res://textures/road.png");
			
			// Пробуем загрузить текстуру из каждого пути
			foreach (string path in possibleRoadPaths)
			{
				if (ResourceLoader.Exists(path))
				{
					Image cachedRoad = LoadImageCached(path, "дороги");
					if (cachedRoad != null && cachedRoad.GetWidth() > 0)
					{
						roadImg = cachedRoad;
						GD.Print($"✅ Текстура дороги загружена из: {path} ({roadImg.GetWidth()}x{roadImg.GetHeight()})");
						loaded = true;
						break;
					}
				}
			}
			
			if (!loaded)
			{
				GD.PrintErr("❌ Не удалось загрузить текстуру дороги из всех попыток!");
				GD.PrintErr("❌ Дороги не будут отображаться без текстуры!");
				roadImg = null;
			}
		}

		// Получаем массивы вершин и UV из первой поверхности ArrayMesh
		var arrays = arrayMesh.SurfaceGetArrays(0);
		Godot.Collections.Array verticesArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];
		Godot.Collections.Array uvArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.TexUV];
		
		if (verticesArray == null || uvArray == null)
		{
			GD.PrintErr("Не удалось получить вершины или UV из ArrayMesh!");
			return;
		}

		// Проверка на пустой массив вершин
		if (verticesArray.Count == 0 || uvArray.Count == 0)
		{
			GD.PrintErr("Массив вершин или UV пуст!");
			return;
		}

		// Определяем размер сетки (число вершин по одной оси)
		int meshRes = (int)Mathf.Sqrt(verticesArray.Count);

		// Создаем карту высот используя UV координаты для правильного маппинга
		// Это необходимо, так как SurfaceTool может изменить порядок вершин
		float[,] heightMap = new float[meshRes, meshRes];
		bool[,] heightMapFilled = new bool[meshRes, meshRes];
		
		// Заполняем карту высот, используя UV координаты для определения позиции
		for (int i = 0; i < verticesArray.Count; i++)
		{
			Vector2 uv = (Vector2)uvArray[i];
			Vector3 vert = (Vector3)verticesArray[i];
			
			// Преобразуем UV (0..1) в координаты сетки
			int x = (int)Mathf.Round(uv.X * (meshRes - 1));
			int z = (int)Mathf.Round(uv.Y * (meshRes - 1));
			
			// Ограничиваем границы
			x = Mathf.Clamp(x, 0, meshRes - 1);
			z = Mathf.Clamp(z, 0, meshRes - 1);
			
			// Если ячейка еще не заполнена или это более точное значение, сохраняем
			if (!heightMapFilled[x, z] || Mathf.Abs(uv.X * (meshRes - 1) - x) < 0.1f)
			{
				heightMap[x, z] = vert.Y;
				heightMapFilled[x, z] = true;
			}
		}
		
		// Заполняем пропущенные ячейки интерполяцией
		for (int z = 0; z < meshRes; z++)
		{
			for (int x = 0; x < meshRes; x++)
			{
				if (!heightMapFilled[x, z])
				{
					// Ищем ближайшие заполненные ячейки
					float sum = 0;
					int count = 0;
					for (int dz = -1; dz <= 1; dz++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							int nx = x + dx;
							int nz = z + dz;
							if (nx >= 0 && nx < meshRes && nz >= 0 && nz < meshRes && heightMapFilled[nx, nz])
							{
								sum += heightMap[nx, nz];
								count++;
							}
						}
					}
					if (count > 0)
					{
						heightMap[x, z] = sum / count;
						heightMapFilled[x, z] = true;
					}
				}
			}
		}

		// Разрешение итоговой текстуры
		// Рассчитываем на основе размера карты для лучшего качества на больших картах
		// Используем формулу: базовое разрешение + дополнительное разрешение в зависимости от размера
		int maxMapSize = Mathf.Max(mapSizeX, mapSizeZ);
		int texRes = TerraConfig.GetTextureResolutionForSize(maxMapSize);
		float tileScale = TerraConfig.GetTileScaleForSize(maxMapSize);
		float tileScaleX = tileScale * (mapSizeX / (float)maxMapSize);
		float tileScaleZ = tileScale * (mapSizeZ / (float)maxMapSize);
		var sandSampler = new TextureSampler(sandImg, texRes, tileScaleX, tileScaleZ);
		var grassSampler = new TextureSampler(grassImg, texRes, tileScaleX, tileScaleZ);
		var rockSampler = new TextureSampler(rockImg, texRes, tileScaleX, tileScaleZ);
		TextureSampler roadSampler = roadImg != null ? new TextureSampler(roadImg, texRes, tileScaleX, tileScaleZ) : null;
		float[,] normalizedSlopeMap = textureMode == 1 ? BuildNormalizedSlopeMap(heightMap, meshRes, maxHeight - minHeight) : null;
		
		GD.Print($"📐 Размер карты: {mapSizeX}x{mapSizeZ}, Разрешение текстуры: {texRes}x{texRes}");
		GD.Print($"🧵 Тайлинг текстур: X={tileScaleX:F2}, Z={tileScaleZ:F2}, base={tileScale:F2}");
		
		// Проверяем маску дорог, если она передана
		if (roadMask != null)
		{
			int maskWidth = roadMask.GetLength(0);
			int maskHeight = roadMask.GetLength(1);
			if (VerboseTextureLogs)
				GD.Print($"🛣️ Маска дорог получена: {maskWidth}x{maskHeight}, ожидается: {texRes}x{texRes}");
			
			// Если размеры не совпадают, это проблема
			if (maskWidth != texRes || maskHeight != texRes)
			{
				GD.PrintErr($"❌ КРИТИЧЕСКАЯ ОШИБКА: Размер маски дорог ({maskWidth}x{maskHeight}) не совпадает с разрешением текстуры ({texRes}x{texRes})!");
				GD.PrintErr("❌ Дороги не будут отображаться корректно!");
			}
			
			if (VerboseTextureLogs)
			{
				int roadPixels = 0;
				float maxMaskValue = 0.0f;
				float minMaskValue = float.MaxValue;
				for (int mx = 0; mx < maskWidth; mx++)
				{
					for (int mz = 0; mz < maskHeight; mz++)
					{
						float val = roadMask[mx, mz];
						if (val > 0.001f)
						{
							roadPixels++;
							if (val > maxMaskValue) maxMaskValue = val;
							if (val < minMaskValue) minMaskValue = val;
						}
					}
				}
				GD.Print($"🛣️ Статистика маски дорог: {roadPixels} пикселей с дорогами из {maskWidth * maskHeight} всего");
				GD.Print($"🛣️ Диапазон значений маски: min={minMaskValue:F3}, max={maxMaskValue:F3}");
			}
		}
		
		// Проверяем загрузку текстуры дороги
		if (roadImg != null && VerboseTextureLogs)
		{
			GD.Print($"✅ Текстура дороги загружена: {roadImg.GetWidth()}x{roadImg.GetHeight()}");
		}
		else if (roadMask != null)
		{
			GD.Print("⚠️ Маска дорог передана, но текстура дороги не загружена!");
		}

		// Создаем пустое изображение с форматом RGBA8
		Image finalImg = Image.CreateEmpty(texRes, texRes, false, Image.Format.Rgba8);

		// Основной цикл по пикселям итоговой текстуры
		int yieldEveryRows = Mathf.Clamp(texRes / 64, 8, 64);
		for (int z = 0; z < texRes; z++)
		{
			if (cancelRequested != null && cancelRequested())
			{
				progressCallback?.Invoke(100.0f, "Генерация остановлена пользователем");
				return;
			}
			for (int x = 0; x < texRes; x++)
			{
				// Преобразуем координаты пикселя в UV координаты (0..1)
				float u = (float)x / (texRes - 1);
				float v = (float)z / (texRes - 1);

				// Преобразуем UV в координаты сетки вершин
				float gridX = u * (meshRes - 1);
				float gridZ = v * (meshRes - 1);

				// Билинейная интерполяция высоты для более плавных переходов
				int x0 = (int)Mathf.Floor(gridX);
				int x1 = (int)Mathf.Ceil(gridX);
				int z0 = (int)Mathf.Floor(gridZ);
				int z1 = (int)Mathf.Ceil(gridZ);

				// Ограничиваем границы
				x0 = Mathf.Clamp(x0, 0, meshRes - 1);
				x1 = Mathf.Clamp(x1, 0, meshRes - 1);
				z0 = Mathf.Clamp(z0, 0, meshRes - 1);
				z1 = Mathf.Clamp(z1, 0, meshRes - 1);

				// Получаем высоты в четырех углах
				float h00 = heightMap[x0, z0];
				float h10 = heightMap[x1, z0];
				float h01 = heightMap[x0, z1];
				float h11 = heightMap[x1, z1];

				// Билинейная интерполяция
				float fx = gridX - x0;
				float fz = gridZ - z0;
				float h0 = Mathf.Lerp(h00, h10, fx);
				float h1 = Mathf.Lerp(h01, h11, fx);
				float height = Mathf.Lerp(h0, h1, fz);

				// Вычисляем нормализованную высоту (0 = низ, 1 = верх)
				float heightRange = maxHeight - minHeight;
				float h = heightRange > 0.001f ? (maxHeight - height) / heightRange : 0.5f;
				h = Mathf.Clamp(h, 0f, 1f);

				// Получаем пиксели с раздельным тайлингом по X/Z, чтобы плотность
				// текстуры была одинаковой на метр даже у вытянутых карт.
				Color sandColor = sandSampler.Sample(x, z);
				Color grassColor = grassSampler.Sample(x, z);
				Color rockColor = rockSampler.Sample(x, z);

				Color finalColor;
				
				if (textureMode == 0)
				{
					// Режим 0: По высоте (песок → трава → камень)
					// Плавные переходы с шириной 0.15 (как в RealWorldTexturePainter)
					float sandToGrassStart = sandGrass - 0.075f;
					float sandToGrassEnd = sandGrass + 0.075f;
					float grassToRockStart = grassRock - 0.075f;
					float grassToRockEnd = grassRock + 0.075f;

					// --- Плавные зоны перехода по высоте ---
					// Ниже sandToGrassStart — чистый песок
					if (h < sandToGrassStart)
						finalColor = sandColor;
					// Между sandToGrassStart и sandToGrassEnd — переход песок -> трава
					else if (h < sandToGrassEnd)
					{
						float t = Mathf.InverseLerp(sandToGrassStart, sandToGrassEnd, h);
						finalColor = sandColor.Lerp(grassColor, t);
					}
					// Между sandToGrassEnd и grassToRockStart — чистая трава
					else if (h < grassToRockStart)
						finalColor = grassColor;
					// Между grassToRockStart и grassToRockEnd — переход трава -> камень
					else if (h < grassToRockEnd)
					{
						float t = Mathf.InverseLerp(grassToRockStart, grassToRockEnd, h);
						finalColor = grassColor.Lerp(rockColor, t);
					}
					// Выше grassToRockEnd — чистый камень
					else
						finalColor = rockColor;
				}
				else
				{
					// Режим 1: камень на склонах (крутизна предвычислена и интерполируется).
					float normalizedSlope = SampleMapBilinear(normalizedSlopeMap, gridX, gridZ);
					
					// Порог для определения склона (настраиваемый) - понижен для лучшей чувствительности
					float slopeThreshold = 0.03f; // 3% от диапазона высот - еще более чувствительный
					
					// Песок-трава по высоте (как обычно)
					float sandToGrassStart = sandGrass - 0.075f;
					float sandToGrassEnd = sandGrass + 0.075f;
					
					// Если это низкая высота (песок), не применяем камень даже на склонах
					if (h < sandToGrassEnd)
					{
						if (h < sandToGrassStart)
							finalColor = sandColor;
						else
						{
							float t = Mathf.InverseLerp(sandToGrassStart, sandToGrassEnd, h);
							finalColor = sandColor.Lerp(grassColor, t);
						}
					}
					else
					{
					// На средних и высоких высотах применяем камень только на склонах
					// Используем плавное смешивание без резкого порога для устранения квадратов
					float minSlope = 0.0f; // Начинаем с нуля для плавного перехода
					float maxSlope = slopeThreshold + 0.15f; // Расширенная зона для более плавного перехода
					
					// Вычисляем фактор склона с плавным переходом (без резкого порога)
					float slopeFactor = 0.0f;
					if (normalizedSlope > minSlope)
					{
						if (normalizedSlope >= maxSlope)
							slopeFactor = 1.0f;
						else
							slopeFactor = Mathf.Clamp(normalizedSlope / maxSlope, 0.0f, 1.0f);
					}
					
					// Используем параметр slopeBlend для настройки плавности перехода
					// slopeBlend = 0.0 -> более резкий переход (степень 0.2)
					// slopeBlend = 1.0 -> очень плавный переход (степень 2.5)
					float blendPower = Mathf.Lerp(0.2f, 2.5f, slopeBlend);
					float rockAmount = Mathf.Pow(slopeFactor, blendPower);
					
					// Минимальное количество камня на склонах зависит от плавности
					// При резком переходе (slopeBlend = 0) минимум выше, при плавном (slopeBlend = 1) ниже
					float minRockAmount = Mathf.Lerp(0.5f, 0.1f, slopeBlend);
					
					// Применяем минимальное количество только если есть хоть какой-то склон
					if (slopeFactor > 0.01f)
					{
						rockAmount = Mathf.Max(rockAmount, minRockAmount * slopeFactor);
					}
					
					// Ограничиваем максимум до 1.0
					rockAmount = Mathf.Clamp(rockAmount, 0.0f, 1.0f);
					
					// Плавное смешивание травы с камнем (всегда смешиваем, но на ровных участках rockAmount = 0)
					finalColor = grassColor.Lerp(rockColor, rockAmount);
					}
				}
				
				// ВАЖНО: Накладываем текстуру дороги ПОСЛЕ всех вычислений основных текстур
				// Дороги должны быть видны четко, без размытия с основными текстурами
				if (roadMask != null && roadImg != null)
				{
					// Проверяем границы маски
					if (x < roadMask.GetLength(0) && z < roadMask.GetLength(1))
					{
						float maskValue = roadMask[x, z];
						if (maskValue > 0.001f) // Используем небольшой порог, чтобы не пропустить слабые значения
						{
							// Получаем цвет дороги из текстуры с tiling
							// Используем то же значение tileScale, что и для основных текстур
							Color roadColor = roadSampler != null ? roadSampler.Sample(x, z) : finalColor;
							
							// ВАЖНО: Дороги должны накладываться с полной силой там, где maskValue близко к 1.0
							// Используем более агрессивное смешивание для лучшей видимости дорог
							// Применяем степенную функцию для усиления маски
							float roadBlend = Mathf.Pow(maskValue, 0.7f); // Усиливаем маску
							roadBlend = Mathf.Clamp(roadBlend, 0.0f, 1.0f);
							
							// Смешиваем основную текстуру с дорогой
							// Используем более сильное смешивание для дорог
							finalColor = finalColor.Lerp(roadColor, roadBlend);
							
							if (VerboseTextureLogs && x < 10 && z < 10 && maskValue > 0.1f)
							{
								GD.Print($"🛣️ Дорога на [{x},{z}]: maskValue={maskValue:F3}, roadBlend={roadBlend:F3}, roadColor={roadColor}, finalColor={finalColor}");
							}
						}
					}
					else
					{
						if (VerboseTextureLogs && x < 5 && z < 5)
						{
							GD.Print($"⚠️ Координаты [{x},{z}] выходят за границы маски {roadMask.GetLength(0)}x{roadMask.GetLength(1)}");
						}
					}
				}

				// Устанавливаем рассчитанный цвет в итоговое изображение
				// ВАЖНО: Это происходит ПОСЛЕ наложения дорог, чтобы дороги были поверх всего
				finalImg.SetPixel(x, z, finalColor);
			}

			if ((z + 1) % yieldEveryRows == 0 || z == texRes - 1)
			{
				float textureProgress = 52.0f + (float)(z + 1) / texRes * 24.0f;
				progressCallback?.Invoke(textureProgress, "Применение текстур...");
				SceneTree tree = meshInstance.GetTree();
				if (tree != null)
					await meshInstance.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
		}

		// Создаем ImageTexture из итогового изображения
		// Используем CreateFromImage с параметрами для лучшего качества
		var tex = ImageTexture.CreateFromImage(finalImg);
		
		// В Godot 4 C# фильтрация настраивается через материал
		// ImageTexture автоматически генерирует мипмапы если включено в настройках проекта

		// Создаем материал и применяем текстуру
		StandardMaterial3D mat = new StandardMaterial3D();
		mat.AlbedoTexture = tex;
		mat.CullMode = BaseMaterial3D.CullModeEnum.Back;
		// Используем стандартную линейную фильтрацию с мипмапами для лучшего качества
		mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;

		// Назначаем материал на MeshInstance3D
		meshInstance.MaterialOverride = mat;

		// Сохраняем изображение на диск
		// Если путь не указан, используем путь по умолчанию в корне проекта
		string finalSavePath = savePath;
		if (string.IsNullOrEmpty(finalSavePath))
		{
			finalSavePath = "res://terrain_texture.png";
		}
		
		Error err = finalImg.SavePng(finalSavePath);
		if (err == Error.Ok)
			GD.Print("Текстура сохранена: ", finalSavePath);
		else
			GD.PrintErr("Ошибка при сохранении текстуры: ", finalSavePath);

		GD.Print("Текстура успешно применена с плавным смешиванием по высоте");
		progressCallback?.Invoke(78.0f, "Текстуры применены");
	}

	private sealed class TextureSampler
	{
		private readonly Image _img;
		private readonly int[] _x0;
		private readonly int[] _x1;
		private readonly float[] _fx;
		private readonly int[] _z0;
		private readonly int[] _z1;
		private readonly float[] _fz;

		public TextureSampler(Image img, int texRes, float tileScaleX, float tileScaleZ)
		{
			_img = img;
			_x0 = new int[texRes];
			_x1 = new int[texRes];
			_fx = new float[texRes];
			_z0 = new int[texRes];
			_z1 = new int[texRes];
			_fz = new float[texRes];
			BuildAxis(texRes, tileScaleX, img.GetWidth(), _x0, _x1, _fx);
			BuildAxis(texRes, tileScaleZ, img.GetHeight(), _z0, _z1, _fz);
		}

		public Color Sample(int x, int z)
		{
			Color c00 = _img.GetPixel(_x0[x], _z0[z]);
			Color c10 = _img.GetPixel(_x1[x], _z0[z]);
			Color c01 = _img.GetPixel(_x0[x], _z1[z]);
			Color c11 = _img.GetPixel(_x1[x], _z1[z]);
			Color c0 = c00.Lerp(c10, _fx[x]);
			Color c1 = c01.Lerp(c11, _fx[x]);
			return c0.Lerp(c1, _fz[z]);
		}

		private static void BuildAxis(int texRes, float tileScale, int imageSize, int[] i0, int[] i1, float[] f)
		{
			for (int p = 0; p < texRes; p++)
			{
				float uv = ((float)p / (texRes - 1)) * tileScale;
				uv -= Mathf.Floor(uv);
				float pixel = uv * (imageSize - 1);
				int a = (int)Mathf.Floor(pixel);
				int b = a + 1;
				if (b >= imageSize) b = 0;
				i0[p] = a;
				i1[p] = b;
				f[p] = pixel - a;
			}
		}
	}

	private static float[,] BuildNormalizedSlopeMap(float[,] heightMap, int meshRes, float heightRange)
	{
		float[,] slopeMap = new float[meshRes, meshRes];
		float invRange = heightRange > 0.001f ? (1.0f / heightRange) : 0.0f;
		for (int z = 0; z < meshRes; z++)
		{
			int z0 = Mathf.Max(0, z - 1);
			int z1 = Mathf.Min(meshRes - 1, z + 1);
			for (int x = 0; x < meshRes; x++)
			{
				int x0 = Mathf.Max(0, x - 1);
				int x1 = Mathf.Min(meshRes - 1, x + 1);
				float gradX = Mathf.Abs(heightMap[x1, z] - heightMap[x0, z]);
				float gradZ = Mathf.Abs(heightMap[x, z1] - heightMap[x, z0]);
				float slope = Mathf.Sqrt(gradX * gradX + gradZ * gradZ);
				slopeMap[x, z] = slope * invRange;
			}
		}
		return slopeMap;
	}

	private static float SampleMapBilinear(float[,] map, float gridX, float gridZ)
	{
		int resX = map.GetLength(0);
		int resZ = map.GetLength(1);
		int x0 = Mathf.Clamp((int)Mathf.Floor(gridX), 0, resX - 1);
		int x1 = Mathf.Clamp((int)Mathf.Ceil(gridX), 0, resX - 1);
		int z0 = Mathf.Clamp((int)Mathf.Floor(gridZ), 0, resZ - 1);
		int z1 = Mathf.Clamp((int)Mathf.Ceil(gridZ), 0, resZ - 1);
		float fx = gridX - x0;
		float fz = gridZ - z0;
		float h00 = map[x0, z0];
		float h10 = map[x1, z0];
		float h01 = map[x0, z1];
		float h11 = map[x1, z1];
		float h0 = Mathf.Lerp(h00, h10, fx);
		float h1 = Mathf.Lerp(h01, h11, fx);
		return Mathf.Lerp(h0, h1, fz);
	}

	private static Image LoadImageCached(string path, string textureLabel)
	{
		if (string.IsNullOrEmpty(path))
		{
			GD.PrintErr($"Путь к текстуре {textureLabel} не указан!");
			return null;
		}

		if (ImageCache.TryGetValue(path, out Image cached) && cached != null && cached.GetWidth() > 0)
			return cached;

		Image img = new Image();
		if (img.Load(path) != Error.Ok)
		{
			GD.PrintErr($"Не удалось загрузить текстуру {textureLabel} по пути: {path}");
			return null;
		}

		ImageCache[path] = img;
		return img;
	}
}
