using Godot;
using System;

// Класс отвечает за генерацию случайного террейна и плоскости воды
public partial class RandomTerrainGenerator : Node
{
	// Метод создаёт меш рельефа с использованием шума
	public Mesh GenerateMesh(
		int length, int width,
		float minHeight, float maxHeight,
		int resolution
	)
	{
		// Создаём генератор шума
		var noise = new FastNoiseLite
		{
			// Используем Перлин-шум для плавных переходов
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			// Частота влияет на детализацию (баланс между гладкостью и деталями)
			Frequency = 0.04f, // Компромисс между плавностью и деталями
			// Используем случайное зерно для разнообразия
			Seed = (int)GD.Randi(),
			// Добавляем фрактальный шум для более естественного вида
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 2, // Уменьшено для более естественного вида (меньше размытия)
			FractalLacunarity = 2.0f,
			FractalGain = 0.6f // Увеличено для более выраженных деталей
		};

		// Генерация меша через MeshBuilder с переданным шумом
		return MeshBuilder.BuildHeightMesh(
			length, width,
			minHeight, maxHeight,
			resolution,
			noise
		);
	}

	// Создание плоскости воды как MeshInstance3D
	public MeshInstance3D GenerateWaterPlane(int length, int width, float waterHeight)
	{
		// Создаём меш воды (простая плоскость)
		var waterMesh = new PlaneMesh
		{
			// Размер плоскости совпадает с размерами террейна
			Size = new Vector2(length, width),
			// Деление плоскости по глубине
			SubdivideDepth = 1,
			// Деление плоскости по ширине
			SubdivideWidth = 1
		};

		// Создаём объект MeshInstance3D для рендера
		var water = new MeshInstance3D
		{
			// Назначаем меш
			Mesh = waterMesh,
			// Имя узла
			Name = "WaterPlane",
			// Размещаем по высоте воды
			Position = new Vector3(0, waterHeight, 0)
		};

		// Создаём материал воды
		var mat = new StandardMaterial3D
		{
			// Цвет воды с прозрачностью
			AlbedoColor = new Color(0.1f, 0.3f, 0.8f, 0.6f),
			// Устанавливаем тип прозрачности
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			// Включаем эффект преломления
			RefractionEnabled = true,
			// Гладкая поверхность
			Roughness = 0.05f,
			// Лёгкое металлическое отражение
			Metallic = 0.2f
		};

		// Применяем материал к воде
		water.MaterialOverride = mat;

		// Возвращаем объект воды
		return water;
	}
}
