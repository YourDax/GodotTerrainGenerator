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
		float grassRock = 0.55f
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
		int texRes = 1024;

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

				// Получаем пиксели из исходных текстур
				Color sandColor = GetSample(sandImg, x, z, texRes);
				Color grassColor = GetSample(grassImg, x, z, texRes);
				Color rockColor = GetSample(rockImg, x, z, texRes);

				Color finalColor;
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
				{
					finalColor = rockColor;
				}

				// Устанавливаем рассчитанный цвет в итоговое изображение
				finalImg.SetPixel(x, z, finalColor);
			}
		}

		// Создаем ImageTexture из итогового изображения
		var tex = ImageTexture.CreateFromImage(finalImg);

		// Создаем материал и применяем текстуру
		StandardMaterial3D mat = new StandardMaterial3D();
		mat.AlbedoTexture = tex;
		mat.CullMode = BaseMaterial3D.CullModeEnum.Back;

		// Назначаем материал на MeshInstance3D
		meshInstance.MaterialOverride = mat;

		// Сохраняем изображение на диск (если указан путь)
		if (!string.IsNullOrEmpty(savePath))
		{
			Error err = finalImg.SavePng(savePath);
			if (err == Error.Ok)
				GD.Print("Текстура сохранена: ", savePath);
			else
				GD.PrintErr("Ошибка при сохранении текстуры: ", savePath);
		}

		GD.Print("Текстура успешно применена с плавным смешиванием по высоте");
	}

	// Вспомогательная функция для выборки пикселя из текстуры по координатам
	private static Color GetSample(Image img, int x, int z, int texRes)
	{
		int tx = Mathf.Clamp((int)((float)x / (texRes - 1) * (img.GetWidth() - 1)), 0, img.GetWidth() - 1);
		int tz = Mathf.Clamp((int)((float)z / (texRes - 1) * (img.GetHeight() - 1)), 0, img.GetHeight() - 1);
		return img.GetPixel(tx, tz);
	}
}
