using Godot;
using System;

[Tool]
public static class RealWorldTexturePainter
{
	// Накладывает текстуру на real-world heightmap, используя реальные значения высот.
	public static void ApplyHeightTexture(
		// Меш, на который накладываем текстуру
		MeshInstance3D meshInstance,
		// Исходный массив высот (в метрах)
		float[,] heights,
		// Разрешение сетки (число вершин по одной оси)
		int meshResX,
		int meshResZ,
		// Путь к текстуре песка
		string sandPath = null,
		// Путь к текстуре травы
		string grassPath = null,
		// Путь к текстуре камня
		string rockPath = null,
		// Граница песок-трава (нормализованная высота 0-1)
		float sandGrass = 0.35f,
		// Граница трава-камень (нормализованная высота 0-1)
		float grassRock = 0.65f
	)
	{
		sandPath = string.IsNullOrWhiteSpace(sandPath) ? TerraConfig.SandTexturePath : sandPath;
		grassPath = string.IsNullOrWhiteSpace(grassPath) ? TerraConfig.GrassTexturePath : grassPath;
		rockPath = string.IsNullOrWhiteSpace(rockPath) ? TerraConfig.RockTexturePath : rockPath;

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

		// Проверка на пустой массив высот
		if (heights == null || heights.GetLength(0) != meshResX || heights.GetLength(1) != meshResZ)
		{
			GD.PrintErr($"Массив высот не соответствует размерам! heights: {heights?.GetLength(0)}x{heights?.GetLength(1)}, expected: {meshResX}x{meshResZ}");
			return;
		}

		// Находим реальные min/max высоты из исходного массива
		float minHeight = float.MaxValue;
		float maxHeight = float.MinValue;
		
		for (int z = 0; z < meshResZ; z++)
		{
			for (int x = 0; x < meshResX; x++)
			{
				float h = heights[x, z];
				if (!float.IsNaN(h))
				{
					if (h < minHeight) minHeight = h;
					if (h > maxHeight) maxHeight = h;
				}
			}
		}
		
		float heightRange = maxHeight - minHeight;
		
		GD.Print($"RealWorldTexturePainter: minHeight={minHeight:F3}, maxHeight={maxHeight:F3}, range={heightRange:F3}");

		// Разрешение итоговой текстуры
		// Рассчитываем на основе размера меша для лучшего качества на больших картах
		int maxRes = Math.Max(meshResX, meshResZ);
		int texRes = TerraConfig.GetTextureResolutionForSize(maxRes);
		
		GD.Print($"Размер меша: {meshResX}x{meshResZ}, Разрешение текстуры: {texRes}x{texRes}");

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
				float gridX = u * (meshResX - 1);
				float gridZ = v * (meshResZ - 1);

				// Билинейная интерполяция высоты из исходного массива
				int x0 = (int)Mathf.Floor(gridX);
				int x1 = (int)Mathf.Ceil(gridX);
				int z0 = (int)Mathf.Floor(gridZ);
				int z1 = (int)Mathf.Ceil(gridZ);

				// Ограничиваем границы
				x0 = Mathf.Clamp(x0, 0, meshResX - 1);
				x1 = Mathf.Clamp(x1, 0, meshResX - 1);
				z0 = Mathf.Clamp(z0, 0, meshResZ - 1);
				z1 = Mathf.Clamp(z1, 0, meshResZ - 1);

				// Получаем высоты в четырех углах из исходного массива
				float h00 = float.IsNaN(heights[x0, z0]) ? minHeight : heights[x0, z0];
				float h10 = float.IsNaN(heights[x1, z0]) ? minHeight : heights[x1, z0];
				float h01 = float.IsNaN(heights[x0, z1]) ? minHeight : heights[x0, z1];
				float h11 = float.IsNaN(heights[x1, z1]) ? minHeight : heights[x1, z1];

				// Билинейная интерполяция
				float fx = gridX - x0;
				float fz = gridZ - z0;
				float h0 = Mathf.Lerp(h00, h10, fx);
				float h1 = Mathf.Lerp(h01, h11, fx);
				float height = Mathf.Lerp(h0, h1, fz);

				// Вычисляем нормализованную высоту (0 = низ, 1 = верх)
				// h = 0 для minHeight (песок), h = 1 для maxHeight (камень)
				float h = heightRange > 0.001f ? (height - minHeight) / heightRange : 0.5f;
				h = Mathf.Clamp(h, 0f, 1f);

				// Получаем пиксели из исходных текстур с tiling для большей детализации
				// Рассчитываем tileScale в зависимости от размера меша для лучшей детализации
				// Для больших мешей увеличиваем количество повторений текстуры
				float tileScale = TerraConfig.GetTileScaleForSize(maxRes);
				
				// Анти-тайлинг: смешиваем 2 выборки с разным масштабом/сдвигом,
				// чтобы сетка повторов и швы были менее заметны.
				Color sandColor = GetSampleAntiTiling(sandImg, x, z, texRes, tileScale);
				Color grassColor = GetSampleAntiTiling(grassImg, x, z, texRes, tileScale);
				Color rockColor = GetSampleAntiTiling(rockImg, x, z, texRes, tileScale);

				Color finalColor;
				// Более широкие и мягкие переходы, чтобы не видно было полос/стыков
				const float transitionWidth = 0.12f;
				float sandToGrassStart = sandGrass - transitionWidth;
				float sandToGrassEnd = sandGrass + transitionWidth;
				float grassToRockStart = grassRock - transitionWidth;
				float grassToRockEnd = grassRock + transitionWidth;

				// --- Плавные зоны перехода по высоте ---
				// Ниже sandToGrassStart — чистый песок
				if (h < sandToGrassStart)
					finalColor = sandColor;
				// Между sandToGrassStart и sandToGrassEnd — переход песок -> трава
				else if (h < sandToGrassEnd)
				{
					float t = Mathf.InverseLerp(sandToGrassStart, sandToGrassEnd, h);
					t = Mathf.SmoothStep(0f, 1f, t);
					finalColor = sandColor.Lerp(grassColor, t);
				}
				// Между sandToGrassEnd и grassToRockStart — чистая трава
				else if (h < grassToRockStart)
					finalColor = grassColor;
				// Между grassToRockStart и grassToRockEnd — переход трава -> камень
				else if (h < grassToRockEnd)
				{
					float t = Mathf.InverseLerp(grassToRockStart, grassToRockEnd, h);
					t = Mathf.SmoothStep(0f, 1f, t);
					finalColor = grassColor.Lerp(rockColor, t);
				}
				// Выше grassToRockEnd — чистый камень
				else
				{
					finalColor = rockColor;
				}

				// Устанавливаем рассчитанный цвет в итоговое изображение
				finalImg.SetPixel(x, z, finalColor);
			}
		}

		// Сохраняем текстуру в PNG для отладки (в корень проекта)
		string debugPath = "res://real_world_terrain_texture_debug.png";
		Error saveErr = finalImg.SavePng(debugPath);
		if (saveErr == Error.Ok)
		{
			GD.Print($"Debug: Текстура сохранена для отладки: {debugPath}");
			GD.Print($"Размер текстуры: {texRes}x{texRes}");
			GD.Print($"Разрешение меша: {meshResX}x{meshResZ}");
			GD.Print($"Диапазон высот вершин: {minHeight:F3} - {maxHeight:F3} (range: {heightRange:F3})");
			GD.Print($"Границы текстур: песок->трава={sandGrass:F2}, трава->камень={grassRock:F2}");
			GD.Print($"Переходы: песок [{sandGrass - 0.075f:F2} - {sandGrass + 0.075f:F2}], трава [{grassRock - 0.075f:F2} - {grassRock + 0.075f:F2}]");
			
			// Выводим несколько примеров высот из исходного массива для отладки
			GD.Print($"Примеры высот из исходного массива:");
			for (int i = 0; i < Math.Min(5, meshResX); i++)
			{
				for (int j = 0; j < Math.Min(5, meshResZ); j++)
				{
					float h = heights[i, j];
					float normalizedH = heightRange > 0.001f ? (h - minHeight) / heightRange : 0.5f;
					GD.Print($"heights[{i},{j}] = {h:F3} (normalized: {normalizedH:F3})");
				}
			}
		}
		else
		{
			GD.PrintErr($"Ошибка при сохранении текстуры для отладки: {saveErr}");
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

		GD.Print("RealWorldTexturePainter: Текстура успешно применена с плавным смешиванием по высоте");
	}

	// Вспомогательная функция для выборки пикселя из текстуры по координатам
	// Используем tiling (повторение) для увеличения детализации
	// Использует билинейную интерполяцию для плавных переходов и скрытия швов
	// Смешивает две тайловые выборки, чтобы скрыть повторяемый рисунок текстуры.
	private static Color GetSampleAntiTiling(Image img, int x, int z, int texRes, float tileScale)
	{
		// Основная выборка
		Color c0 = GetSample(img, x, z, texRes, tileScale);
		// Вторая выборка: другой масштаб + смещение, чтобы ломать регулярную сетку
		int ox = (x + (texRes / 3)) % texRes;
		int oz = (z + (texRes / 5)) % texRes;
		Color c1 = GetSample(img, ox, oz, texRes, tileScale * 1.73f);

		// Низкочастотный шум-смесь (детерминированно от координат)
		float n = Hash01(x, z);
		float mix = Mathf.Lerp(0.28f, 0.62f, n);
		return c0.Lerp(c1, mix);
	}

	// Возвращает детерминированный шум в диапазоне 0..1 для смешивания выборок.
	private static float Hash01(int x, int z)
	{
		// Простой стабильный hash -> [0..1]
		int h = x * 73856093 ^ z * 19349663;
		h = (h << 13) ^ h;
		float v = 1.0f - ((h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f;
		return Mathf.Clamp((v + 1f) * 0.5f, 0f, 1f);
	}

	// Берёт цвет из текстуры с повторением и билинейной интерполяцией.
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
