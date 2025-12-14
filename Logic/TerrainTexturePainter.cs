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

		// Загружаем текстуру песка
		Image sandImg = new Image();
		if (sandImg.Load(sandPath) != Error.Ok)
		{
			GD.PrintErr("Не удалось загрузить текстуру песка!");
			return;
		}

		// Загружаем текстуру травы
		Image grassImg = new Image();
		if (grassImg.Load(grassPath) != Error.Ok)
		{
			GD.PrintErr("Не удалось загрузить текстуру травы!");
			return;
		}

		// Загружаем текстуру камня
		Image rockImg = new Image();
		if (rockImg.Load(rockPath) != Error.Ok)
		{
			GD.PrintErr("Не удалось загрузить текстуру камня!");
			return;
		}

		// Получаем массив вершин из первой поверхности ArrayMesh
		var arrays = arrayMesh.SurfaceGetArrays(0);
		Godot.Collections.Array verticesArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];
		if (verticesArray == null)
		{
			GD.PrintErr("Не удалось получить вершины из ArrayMesh!");
			return;
		}

		// Определяем размер сетки (число вершин по одной оси)
		int meshRes = (int)Mathf.Sqrt(verticesArray.Count);

		// Разрешение итоговой текстуры
		int texRes = 1024;

		// Создаем пустое изображение с форматом RGBA8
		Image finalImg = Image.CreateEmpty(texRes, texRes, false, Image.Format.Rgba8);

		// Основной цикл по пикселям итоговой текстуры
		for (int z = 0; z < texRes; z++)
		{
			for (int x = 0; x < texRes; x++)
			{
				// Преобразуем координаты пикселя в индексы вершин меша
				float vx = (float)x / (texRes - 1) * (meshRes - 1);
				float vz = (float)z / (texRes - 1) * (meshRes - 1);

				int ix = (int)Mathf.Clamp(Mathf.Round(vx), 0, meshRes - 1);
				int iz = (int)Mathf.Clamp(Mathf.Round(vz), 0, meshRes - 1);
				int vertIndex = iz * meshRes + ix;

				// Получаем вершину по индексу
				Vector3 vert = (Vector3)verticesArray[vertIndex];

				// Вычисляем нормализованную высоту (0 = низ, 1 = верх)
				float h = (maxHeight - vert.Y) / (maxHeight - minHeight);
				h = Mathf.Clamp(h, 0f, 1f);

				// Получаем пиксели из исходных текстур
				Color sandColor = GetSample(sandImg, x, z, texRes);
				Color grassColor = GetSample(grassImg, x, z, texRes);
				Color rockColor = GetSample(rockImg, x, z, texRes);

				Color finalColor;
				float sandToGrassStart = sandGrass - 0.1f;
				float sandToGrassEnd = sandGrass + 0.1f;
				float grassToRockStart = grassRock - 0.1f;
				float grassToRockEnd = grassRock + 0.1f;

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
