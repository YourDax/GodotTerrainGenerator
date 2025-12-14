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
		// Создаём массив вершин фиксированного размера
		var vertices = new Vector3[resolution * resolution];
		// Массив индексов для треугольников (6 индексов на одну квадратную ячейку)
		var indices = new int[(resolution - 1) * (resolution - 1) * 6];
		// Массив UV координат для текстурирования
		var uvs = new Vector2[resolution * resolution];

		// Индекс текущей вершины
		int v = 0;

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

				// Получение шума по координатам
				float n = noise.GetNoise2D(wx, wz);
				// Преобразование шума [-1..1] в высоту [minHeight..maxHeight]
				float height = Mathf.Lerp(minHeight, maxHeight, (n + 1f) * 0.5f);

				// Добавление вершины в массив
				vertices[v] = new Vector3(wx, height, wz);
				// Запись UV координат
				uvs[v] = new Vector2(px, pz);

				// Переход к следующей вершине
				v++;
			}
		}

		// Индекс для записи треугольников
		int t = 0;

		// Генерация индексов треугольников
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
				indices[t++] = a;
				indices[t++] = b;
				indices[t++] = c;

				// Второй треугольник (c, b, d)
				indices[t++] = c;
				indices[t++] = b;
				indices[t++] = d;
			}
		}

		// Создаём Array для GodotMesh
		var arrays = new Godot.Collections.Array();
		// Размер задаётся по количеству типов данных, требуемых Mesh
		arrays.Resize((int)Mesh.ArrayType.Max);

		// Записываем массивы в структуру для меша
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Index] = indices;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;

		// Создаём меш и добавляем поверхность
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

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
