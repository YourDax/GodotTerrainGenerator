using Godot;

// Класс для процедурной генерации мешей
public static class MeshBuilder
{
	// Генерация меша на основе шума высот (старый метод для обратной совместимости)
	public static Mesh BuildHeightMesh(
		int length, int width,
		float minHeight, float maxHeight,
		int resolution,
		FastNoiseLite noise,
		float smoothing = 1.0f
	)
	{
		// Создаем простые шумы для обратной совместимости
		return BuildHeightMesh(
			length, width,
			minHeight, maxHeight,
			resolution,
			noise,
			noise,
			noise,
			smoothing
		);
	}
	
	// Генерация меша на основе многослойного шума для реалистичного ландшафта
	public static Mesh BuildHeightMesh(
		int length, int width,
		float minHeight, float maxHeight,
		int resolution,
		FastNoiseLite baseNoise,
		FastNoiseLite hillNoise,
		FastNoiseLite detailNoise,
		float smoothing = 1.0f
	)
	{
		// Используем SurfaceTool для более гладкого меша с нормалями
		SurfaceTool st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		// Массивы для хранения вершин и UV (для правильной индексации)
		Vector3[] vertices = new Vector3[resolution * resolution];
		Vector2[] uvs = new Vector2[resolution * resolution];

		// Генерация сетки вершин
		for (int z = 0; z < resolution; z++)
		{
			for (int x = 0; x < resolution; x++)
			{
				// Нормализованные координаты сетки (0..1)
				float px = (float)x / (resolution - 1);
				float pz = (float)z / (resolution - 1);

				// Преобразование в мировые координаты
				float wx = px * length - length / 2f;
				float wz = pz * width - width / 2f;

				// Многослойная система шума для реалистичного ландшафта
				// 1. Крупномасштабный шум - основные формы рельефа (горы, долины)
				// Это создает основную структуру ландшафта
				float baseNoiseValue = baseNoise.GetNoise2D(wx, wz);
				
				// Применяем степенную функцию для более плавных крупных форм
				// Это делает горы более округлыми, а долины более широкими
				baseNoiseValue = Mathf.Sign(baseNoiseValue) * Mathf.Pow(Mathf.Abs(baseNoiseValue), 0.7f);
				
				// 2. Среднемасштабный шум - холмы и впадины
				// Добавляет разнообразие к крупным формам
				float hills = hillNoise.GetNoise2D(wx, wz) * 0.4f; // 40% влияния
				
				// 3. Мелкомасштабный шум - детали поверхности
				// Добавляет мелкие неровности, но только если smoothing > 0
				float details = detailNoise.GetNoise2D(wx, wz) * 0.15f * smoothing; // 15% влияния, зависит от smoothing
				
				// Комбинируем все слои
				// Крупные формы имеют наибольший вес, детали - наименьший
				float n = baseNoiseValue + hills + details;
				
				// Применяем дополнительное сглаживание для больших карт
				// Это создает более естественные переходы между высотами
				int maxSize = Mathf.Max(length, width);
				if (maxSize > 200)
				{
					// Для больших карт применяем дополнительное сглаживание
					// Используем степенную функцию для более плавных переходов
					n = Mathf.Sign(n) * Mathf.Pow(Mathf.Abs(n), 0.85f);
				}
				
				// Нормализуем результат обратно в диапазон [-1, 1]
				n = Mathf.Clamp(n, -1.0f, 1.0f);
				
				// Преобразование шума [-1..1] в высоту [minHeight..maxHeight]
				float height = Mathf.Lerp(minHeight, maxHeight, (n + 1f) * 0.5f);

				// Сохраняем вершину и UV
				int idx = z * resolution + x;
				vertices[idx] = new Vector3(wx, height, wz);
				uvs[idx] = new Vector2(px, pz);
			}
		}

		// Генерация треугольников с использованием SurfaceTool
		for (int z = 0; z < resolution - 1; z++)
		{
			for (int x = 0; x < resolution - 1; x++)
			{
				// Индексы углов текущего квадрата
				int a = z * resolution + x;
				int b = a + resolution;
				int c = a + 1;
				int d = b + 1;

				// Первый треугольник (a, b, c)
				st.SetUV(uvs[a]);
				st.AddVertex(vertices[a]);
				st.SetUV(uvs[b]);
				st.AddVertex(vertices[b]);
				st.SetUV(uvs[c]);
				st.AddVertex(vertices[c]);

				// Второй треугольник (c, b, d)
				st.SetUV(uvs[c]);
				st.AddVertex(vertices[c]);
				st.SetUV(uvs[b]);
				st.AddVertex(vertices[b]);
				st.SetUV(uvs[d]);
				st.AddVertex(vertices[d]);
			}
		}

		// Генерируем нормали для сглаживания поверхности
		// Если сглаживание меньше 1.0, применяем дополнительное сглаживание к вершинам
		if (smoothing < 1.0f)
		{
			// Применяем простое сглаживание вершин для более плавного рельефа
			// Проходим по внутренним вершинам и сглаживаем их высоту
			for (int iteration = 0; iteration < Mathf.RoundToInt((1.0f - smoothing) * 3.0f); iteration++)
			{
				Vector3[] smoothedVertices = new Vector3[vertices.Length];
				System.Array.Copy(vertices, smoothedVertices, vertices.Length);
				
				for (int z = 1; z < resolution - 1; z++)
				{
					for (int x = 1; x < resolution - 1; x++)
					{
						int idx = z * resolution + x;
						
						// Берем среднее значение высот соседних вершин
						float avgHeight = (
							vertices[idx - 1].Y + // левый
							vertices[idx + 1].Y + // правый
							vertices[idx - resolution].Y + // верхний
							vertices[idx + resolution].Y // нижний
						) / 4.0f;
						
						// Интерполируем между исходной и сглаженной высотой
						float originalHeight = vertices[idx].Y;
						smoothedVertices[idx] = new Vector3(
							vertices[idx].X,
							Mathf.Lerp(originalHeight, avgHeight, 0.5f * (1.0f - smoothing)),
							vertices[idx].Z
						);
					}
				}
				
				vertices = smoothedVertices;
			}
			
			// Обновляем вершины в SurfaceTool
			st = new SurfaceTool();
			st.Begin(Mesh.PrimitiveType.Triangles);
			
			// Пересоздаем треугольники с обновленными вершинами
			for (int z = 0; z < resolution - 1; z++)
			{
				for (int x = 0; x < resolution - 1; x++)
				{
					int a = z * resolution + x;
					int b = a + resolution;
					int c = a + 1;
					int d = b + 1;

					st.SetUV(uvs[a]);
					st.AddVertex(vertices[a]);
					st.SetUV(uvs[b]);
					st.AddVertex(vertices[b]);
					st.SetUV(uvs[c]);
					st.AddVertex(vertices[c]);

					st.SetUV(uvs[c]);
					st.AddVertex(vertices[c]);
					st.SetUV(uvs[b]);
					st.AddVertex(vertices[b]);
					st.SetUV(uvs[d]);
					st.AddVertex(vertices[d]);
				}
			}
		}
		
		// Генерируем нормали для сглаживания поверхности
		st.GenerateNormals();

		// Финализируем меш
		ArrayMesh mesh = st.Commit();

		// Возвращаем готовый меш
		return mesh;
	}

	// Создание меша на основе существующей heightmap
	public static Mesh BuildTerrainMesh(float[,] heightmap, int length, int width)
	{
		// Получение размерностей массива
		int sizeX = heightmap.GetLength(0);
		int sizeZ = heightmap.GetLength(1);

		// Расстояние между соседними вершинами по осям
		float stepX = (float)length / (sizeX - 1);
		float stepZ = (float)width / (sizeZ - 1);

		// SurfaceTool — удобный способ вручную собирать меш
		SurfaceTool st = new SurfaceTool();
		// Начинаем запись треугольников
		st.Begin(Mesh.PrimitiveType.Triangles);

		// Генерация всех вершин
		for (int z = 0; z < sizeZ; z++)
		{
			for (int x = 0; x < sizeX; x++)
			{
				// Формирование позиции вершины с использованием heightmap
				Vector3 pos = new Vector3(
					x * stepX,
					heightmap[x, z],
					z * stepZ
				);

				// Вычисление UV координат (0..1)
				Vector2 uv = new Vector2(
					(float)x / (sizeX - 1),
					(float)z / (sizeZ - 1)
				);

				// Установка UV перед добавлением вершины
				st.SetUV(uv);
				// Добавление вершины в SurfaceTool
				st.AddVertex(pos);
			}
		}

		// Генерация индексов треугольников
		for (int z = 0; z < sizeZ - 1; z++)
		{
			for (int x = 0; x < sizeX - 1; x++)
			{
				// Базовый индекс для текущей клетки
				int i = z * sizeX + x;

				// Четыре угла квадрата
				int i0 = i;
				int i1 = i + 1;
				int i2 = i + sizeX;
				int i3 = i + sizeX + 1;

				// Первый треугольник
				st.AddIndex(i0);
				st.AddIndex(i2);
				st.AddIndex(i1);

				// Второй треугольник
				st.AddIndex(i1);
				st.AddIndex(i2);
				st.AddIndex(i3);
			}
		}

		// Автоматическое вычисление нормалей (для освещения)
		st.GenerateNormals();

		// Финализируем меш
		ArrayMesh mesh = st.Commit();

		// Возвращаем результат
		return mesh;
	}
}
