using Godot;
using System;

// Класс отвечает за генерацию случайного террейна и плоскости воды
public partial class RandomTerrainGenerator : Node
{
	// Метод создаёт меш рельефа с использованием шума
	// Генерирует процедурный меш террейна на основе нескольких слоёв шума.
	public Mesh GenerateMesh(
		int length, int width,
		float minHeight, float maxHeight,
		int resolution,
		float smoothing = 1.0f,
		bool generateIsland = false,
		float waterLevel01 = 0.35f,
		int baseSeed = -1,
		int hillSeed = -1,
		int detailSeed = -1,
		int coastSeed = -1,
		float noiseSampleOffsetX = 0f,
		float noiseSampleOffsetZ = 0f
	)
	{
		// Вычисляем размер карты для адаптивной генерации
		int maxSize = Math.Max(length, width);
		
		// Создаём несколько генераторов шума для разных масштабов
		// Это создаст более реалистичный ландшафт с крупными формами и деталями
		
		// 1. Крупномасштабный шум для основных форм рельефа (горы, долины)
		// Частота адаптируется к размеру карты - на больших картах более низкая частота
		float baseFrequency = 1.0f / (maxSize * 0.5f); // Адаптивная частота
		baseFrequency = Mathf.Clamp(baseFrequency, 0.001f, 0.02f); // Ограничиваем диапазон
		
		int actualBaseSeed = baseSeed >= 0 ? baseSeed : (int)GD.Randi();
		int actualHillSeed = hillSeed >= 0 ? hillSeed : (int)GD.Randi() + 1000;
		int actualDetailSeed = detailSeed >= 0 ? detailSeed : (int)GD.Randi() + 2000;
		int actualCoastSeed = coastSeed >= 0 ? coastSeed : (int)GD.Randi() + 9001;

		var baseNoise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = baseFrequency,
			Seed = actualBaseSeed,
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 4, // Больше октав для более плавных крупных форм
			FractalLacunarity = 2.0f,
			FractalGain = 0.5f // Меньший gain для более плавных переходов
		};
		
		// 2. Среднемасштабный шум для холмов и впадин
		var hillNoise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = baseFrequency * 3.0f, // В 3 раза выше частота
			Seed = actualHillSeed, // Другое зерно
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 3,
			FractalLacunarity = 2.0f,
			FractalGain = 0.5f
		};
		
		// 3. Мелкомасштабный шум для деталей (только если smoothing > 0)
		var detailNoise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = baseFrequency * 8.0f, // В 8 раз выше частота
			Seed = actualDetailSeed, // Другое зерно
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 2,
			FractalLacunarity = 2.0f,
			FractalGain = 0.4f
		};

		FastNoiseLite coastNoise = null;
		float islandCoastWidth = 0.2f;
		float islandCliffExp = 1f;
		float islandNoiseAmp = 0.06f;
		float islandSeabedFrac = 0.18f;
		if (generateIsland)
		{
			var islandRng = new RandomNumberGenerator();
			islandRng.Seed = (ulong)actualCoastSeed;
			coastNoise = new FastNoiseLite
			{
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				Frequency = 0.08f,
				Seed = actualCoastSeed,
				FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
				FractalOctaves = 2,
				FractalLacunarity = 2.0f,
				FractalGain = 0.45f
			};
			islandCoastWidth = Mathf.Lerp(0.10f, 0.32f, islandRng.Randf());
			islandCliffExp = Mathf.Lerp(0.45f, 2.9f, islandRng.Randf());
			islandNoiseAmp = Mathf.Lerp(0.025f, 0.11f, islandRng.Randf());
			islandSeabedFrac = Mathf.Lerp(0.14f, 0.28f, islandRng.Randf());
		}

		// Генерация меша через MeshBuilder с переданными шумами
		return MeshBuilder.BuildHeightMesh(
			length, width,
			minHeight, maxHeight,
			resolution,
			baseNoise,
			hillNoise,
			detailNoise,
			smoothing,
			generateIsland,
			waterLevel01,
			islandCoastWidth,
			islandCliffExp,
			islandNoiseAmp,
			islandSeabedFrac,
			coastNoise,
			noiseSampleOffsetX,
			noiseSampleOffsetZ
		);
	}

	// Создание плоскости воды как MeshInstance3D
	// Создаёт отдельный MeshInstance3D для плоскости воды на нужной высоте.
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
		water.SetMeta("terrain_is_water", true);

		// Возвращаем объект воды
		return water;
	}
}
