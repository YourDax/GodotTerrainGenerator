using Godot;

// Класс для процедурной генерации мешей
public static class MeshBuilder
{
	// Генерация меша на основе шума высот
	public static Mesh BuildHeightMesh(
		int length, int width,
		float minHeight, float maxHeight,
		int resolution,
		FastNoiseLite noise
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

				// Получение шума по координатам (основной рельеф)
				float n = noise.GetNoise2D(wx, wz);
				
				// Добавляем дополнительный слой деталей для более естественного вида
				// Используем более высокую частоту для мелких деталей
				float detailNoise = noise.GetNoise2D(wx * 3.0f, wz * 3.0f) * 0.15f;
				n += detailNoise;
				
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
		// Используем параметр smooth=true для плавных переходов, но не слишком размытых
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
