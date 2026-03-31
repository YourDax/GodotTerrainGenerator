using Godot;
using System;

[Tool]
public static class TerrainTexturePainter
{
	public static void ApplyHeightTexture(
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
		string roadPath = null
	)
	{
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

		// Загружаем текстуру песка
		Image sandImg = new Image();
		if (sandImg.Load(sandPath) != Error.Ok)
		{
			GD.PrintErr($"Не удалось загрузить текстуру песка по пути: {sandPath}");
			return;
		}

		// Загружаем текстуру травы
		Image grassImg = new Image();
		if (grassImg.Load(grassPath) != Error.Ok)
		{
			GD.PrintErr($"Не удалось загрузить текстуру травы по пути: {grassPath}");
			return;
		}

		// Загружаем текстуру камня
		Image rockImg = new Image();
		if (rockImg.Load(rockPath) != Error.Ok)
		{
			GD.PrintErr($"Не удалось загрузить текстуру камня по пути: {rockPath}");
			return;
		}
		
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
			possibleRoadPaths.Add("res://addons/terragenerating/Texture/road.jpg");
			possibleRoadPaths.Add("res://addons/terragenerating/Texture/road.png");
			possibleRoadPaths.Add("res://Texture/road.jpg");
			possibleRoadPaths.Add("res://Texture/road.png");
			possibleRoadPaths.Add("res://textures/road.jpg");
			possibleRoadPaths.Add("res://textures/road.png");
			
			// Пробуем загрузить текстуру из каждого пути
			foreach (string path in possibleRoadPaths)
			{
				GD.Print($"🔍 Пробую загрузить текстуру дороги из: {path}");
				if (ResourceLoader.Exists(path))
				{
					GD.Print($"✅ Путь существует: {path}");
					if (roadImg.Load(path) == Error.Ok && roadImg.GetWidth() > 0)
					{
						GD.Print($"✅ Текстура дороги загружена из: {path} ({roadImg.GetWidth()}x{roadImg.GetHeight()})");
						loaded = true;
						break;
					}
					else
					{
						GD.Print($"⚠️ Путь существует, но не удалось загрузить изображение: {path}");
					}
				}
				else
				{
					GD.Print($"❌ Путь не существует: {path}");
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
		int texRes = 1024; // Базовое разрешение
		
		// Увеличиваем разрешение пропорционально размеру карты
		if (maxMapSize > 500)
		{
			texRes = 4096; // Максимальное разрешение для очень больших карт
		}
		else if (maxMapSize > 300)
		{
			texRes = 3072; // Высокое разрешение для больших карт
		}
		else if (maxMapSize > 200)
		{
			texRes = 2048; // Средне-высокое разрешение
		}
		else if (maxMapSize > 100)
		{
			texRes = 1536; // Среднее разрешение
		}
		else if (maxMapSize > 50)
		{
			texRes = 1280; // Небольшое увеличение для средних карт
		}
		
		GD.Print($"📐 Размер карты: {mapSizeX}x{mapSizeZ}, Разрешение текстуры: {texRes}x{texRes}");
		
		// Проверяем маску дорог, если она передана
		if (roadMask != null)
		{
			int maskWidth = roadMask.GetLength(0);
			int maskHeight = roadMask.GetLength(1);
			GD.Print($"🛣️ Маска дорог получена: {maskWidth}x{maskHeight}, ожидается: {texRes}x{texRes}");
			
			// Если размеры не совпадают, это проблема
			if (maskWidth != texRes || maskHeight != texRes)
			{
				GD.PrintErr($"❌ КРИТИЧЕСКАЯ ОШИБКА: Размер маски дорог ({maskWidth}x{maskHeight}) не совпадает с разрешением текстуры ({texRes}x{texRes})!");
				GD.PrintErr("❌ Дороги не будут отображаться корректно!");
			}
			
			// Подсчитываем количество пикселей дорог в маске
			int roadPixels = 0;
			float maxMaskValue = 0.0f;
			float minMaskValue = float.MaxValue;
			// Проверяем всю маску для точной статистики
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
			
			// Проверяем несколько конкретных точек для отладки
			if (roadPixels > 0)
			{
				int sampleCount = 0;
				for (int mx = 0; mx < maskWidth && sampleCount < 5; mx += maskWidth / 10)
				{
					for (int mz = 0; mz < maskHeight && sampleCount < 5; mz += maskHeight / 10)
					{
						if (roadMask[mx, mz] > 0.001f)
						{
							GD.Print($"🛣️ Пример дороги на [{mx},{mz}]: значение={roadMask[mx, mz]:F3}");
							sampleCount++;
						}
					}
				}
			}
			else
			{
				GD.PrintErr("❌ В маске дорог нет ни одного пикселя с дорогами!");
			}
		}
		
		// Проверяем загрузку текстуры дороги
		if (roadImg != null)
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
		for (int z = 0; z < texRes; z++)
		{
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

				// Получаем пиксели из исходных текстур с tiling для большей детализации
				// Рассчитываем tileScale в зависимости от размера карты для лучшей детализации
				// Для больших карт увеличиваем количество повторений текстуры
				float tileScale = 4.0f; // Базовое количество повторений
				if (maxMapSize > 500)
				{
					tileScale = 16.0f; // Много повторений для очень больших карт
				}
				else if (maxMapSize > 300)
				{
					tileScale = 12.0f; // Много повторений для больших карт
				}
				else if (maxMapSize > 200)
				{
					tileScale = 10.0f; // Средне-много повторений
				}
				else if (maxMapSize > 100)
				{
					tileScale = 8.0f; // Среднее количество повторений
				}
				else if (maxMapSize > 50)
				{
					tileScale = 6.0f; // Небольшое увеличение для средних карт
				}
				
				Color sandColor = GetSample(sandImg, x, z, texRes, tileScale);
				Color grassColor = GetSample(grassImg, x, z, texRes, tileScale);
				Color rockColor = GetSample(rockImg, x, z, texRes, tileScale);

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
					// Режим 1: Камень только на склонах гор
					// Вычисляем градиент высоты для определения склонов
					// Используем усреднение по большей области для более плавного результата
					
					float gradX = 0.0f, gradZ = 0.0f;
					float gradXSum = 0.0f, gradZSum = 0.0f;
					int gradCount = 0;
					
					// Вычисляем градиент, усредняя по области 3x3 для более плавного результата
					for (int dz = -1; dz <= 1; dz++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							int nx0 = Mathf.Clamp(x0 + dx, 0, meshRes - 1);
							int nx1 = Mathf.Clamp(x1 + dx, 0, meshRes - 1);
							int nz0 = Mathf.Clamp(z0 + dz, 0, meshRes - 1);
							int nz1 = Mathf.Clamp(z1 + dz, 0, meshRes - 1);
							
							// Вычисляем высоту в этой точке
							float nh00 = heightMap[nx0, nz0];
							float nh10 = heightMap[nx1, nz0];
							float nh01 = heightMap[nx0, nz1];
							float nh11 = heightMap[nx1, nz1];
							
							float nh0 = Mathf.Lerp(nh00, nh10, fx);
							float nh1 = Mathf.Lerp(nh01, nh11, fx);
							float nHeight = Mathf.Lerp(nh0, nh1, fz);
							
							// Вычисляем градиент по X для этой точки
							if (nx0 > 0 && nx1 < meshRes - 1)
							{
								float hLeft = heightMap[nx0 - 1, nz0];
								float hRight = heightMap[Mathf.Min(nx1 + 1, meshRes - 1), nz0];
								gradXSum += Mathf.Abs(hRight - hLeft);
								gradCount++;
							}
							
							// Вычисляем градиент по Z для этой точки
							if (nz0 > 0 && nz1 < meshRes - 1)
							{
								float hUp = heightMap[nx0, nz0 - 1];
								float hDown = heightMap[nx0, Mathf.Min(nz1 + 1, meshRes - 1)];
								gradZSum += Mathf.Abs(hDown - hUp);
							}
						}
					}
					
					// Усредняем градиенты
					if (gradCount > 0)
					{
						gradX = gradXSum / gradCount;
						gradZ = gradZSum / gradCount;
					}
					else
					{
						// Fallback на простое вычисление, если усреднение не удалось
						if (x0 > 0 && x1 < meshRes - 1)
						{
							float hLeft = Mathf.Lerp(heightMap[x0 - 1, z0], heightMap[x0 - 1, z1], fz);
							float hRight = Mathf.Lerp(heightMap[x1 + 1, z0], heightMap[x1 + 1, z1], fz);
							gradX = Mathf.Abs(hRight - hLeft);
						}
						if (z0 > 0 && z1 < meshRes - 1)
						{
							float hUp = Mathf.Lerp(heightMap[x0, z0 - 1], heightMap[Mathf.Min(x1, meshRes - 1), z0 - 1], fx);
							float hDown = Mathf.Lerp(heightMap[x0, z1 + 1], heightMap[Mathf.Min(x1, meshRes - 1), z1 + 1], fx);
							gradZ = Mathf.Abs(hDown - hUp);
						}
					}
					
					// Вычисляем общий градиент (крутизну склона)
					float slope = Mathf.Sqrt(gradX * gradX + gradZ * gradZ);
					
					// Нормализуем градиент относительно диапазона высот
					// Используем уже объявленную переменную heightRange
					float normalizedSlope = heightRange > 0.001f ? slope / heightRange : 0.0f;
					
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
							Color roadColor = GetSample(roadImg, x, z, texRes, tileScale);
							
							// ВАЖНО: Дороги должны накладываться с полной силой там, где maskValue близко к 1.0
							// Используем более агрессивное смешивание для лучшей видимости дорог
							// Применяем степенную функцию для усиления маски
							float roadBlend = Mathf.Pow(maskValue, 0.7f); // Усиливаем маску
							roadBlend = Mathf.Clamp(roadBlend, 0.0f, 1.0f);
							
							// Смешиваем основную текстуру с дорогой
							// Используем более сильное смешивание для дорог
							finalColor = finalColor.Lerp(roadColor, roadBlend);
							
							// Отладочный вывод для первых нескольких пикселей дорог
							if (x < 10 && z < 10 && maskValue > 0.1f)
							{
								GD.Print($"🛣️ Дорога на [{x},{z}]: maskValue={maskValue:F3}, roadBlend={roadBlend:F3}, roadColor={roadColor}, finalColor={finalColor}");
							}
						}
					}
					else
					{
						// Отладочный вывод, если координаты выходят за границы
						if (x < 5 && z < 5)
						{
							GD.Print($"⚠️ Координаты [{x},{z}] выходят за границы маски {roadMask.GetLength(0)}x{roadMask.GetLength(1)}");
						}
					}
				}

				// Устанавливаем рассчитанный цвет в итоговое изображение
				// ВАЖНО: Это происходит ПОСЛЕ наложения дорог, чтобы дороги были поверх всего
				finalImg.SetPixel(x, z, finalColor);
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
	}

	// Вспомогательная функция для выборки пикселя из текстуры по координатам
	// Используем tiling (повторение) для увеличения детализации
	// Использует билинейную интерполяцию для плавных переходов и скрытия швов
	private static Color GetSample(Image img, int x, int z, int texRes, float tileScale = 4.0f)
	{
		// Применяем tiling - текстура повторяется несколько раз
		// tileScale определяет, сколько раз текстура повторяется по поверхности
		float u = ((float)x / (texRes - 1)) * tileScale;
		float v = ((float)z / (texRes - 1)) * tileScale;
		
		// Используем модуль для создания повторяющегося паттерна
		u = u - Mathf.Floor(u);
		v = v - Mathf.Floor(v);
		
		// Преобразуем в координаты текстуры (0..1)
		float texU = u;
		float texV = v;
		
		// Преобразуем в пиксельные координаты текстуры
		float pixelU = texU * (img.GetWidth() - 1);
		float pixelV = texV * (img.GetHeight() - 1);
		
		// Получаем целочисленные координаты для билинейной интерполяции
		int tx0 = (int)Mathf.Floor(pixelU);
		int tx1 = tx0 + 1;
		int tz0 = (int)Mathf.Floor(pixelV);
		int tz1 = tz0 + 1;
		
		// Ограничиваем границы с учетом зацикливания (tiling)
		tx0 = tx0 % img.GetWidth();
		tx1 = tx1 % img.GetWidth();
		tz0 = tz0 % img.GetHeight();
		tz1 = tz1 % img.GetHeight();
		
		// Обрабатываем отрицательные значения
		if (tx0 < 0) tx0 += img.GetWidth();
		if (tx1 < 0) tx1 += img.GetWidth();
		if (tz0 < 0) tz0 += img.GetHeight();
		if (tz1 < 0) tz1 += img.GetHeight();
		
		// Получаем цвета в четырех углах
		Color c00 = img.GetPixel(tx0, tz0);
		Color c10 = img.GetPixel(tx1, tz0);
		Color c01 = img.GetPixel(tx0, tz1);
		Color c11 = img.GetPixel(tx1, tz1);
		
		// Вычисляем дробные части для интерполяции
		float fx = pixelU - Mathf.Floor(pixelU);
		float fz = pixelV - Mathf.Floor(pixelV);
		
		// Билинейная интерполяция для плавного перехода
		Color c0 = c00.Lerp(c10, fx);
		Color c1 = c01.Lerp(c11, fx);
		Color finalColor = c0.Lerp(c1, fz);
		
		return finalColor;
	}
}
