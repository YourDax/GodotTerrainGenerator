using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Tool]
public partial class TerrainGenerator : Node3D
{
	private const bool ContinuationDebugLogging = true;
	private const string MetaNoiseSeedBase = "terrain_noise_seed_base";
	private const string MetaNoiseSeedHill = "terrain_noise_seed_hill";
	private const string MetaNoiseSeedDetail = "terrain_noise_seed_detail";
	private const string MetaNoiseSeedCoast = "terrain_noise_seed_coast";
	private bool _cancelRequested = false;

	// Сигнал для обновления прогресса
	[Signal]
	public delegate void ProgressUpdatedEventHandler(float progress, string status);

	public void GenerateFromConfig(Godot.Collections.Dictionary config)
	{
		if (config == null)
		{
			GD.PrintErr("GenerateFromConfig: config is null");
			return;
		}

		Generate(
			GetInt(config, "length", 100),
			GetInt(config, "width", 100),
			GetFloat(config, "min_height", 0f),
			GetFloat(config, "max_height", 25f),
			GetFloat(config, "sand_grass", 0.35f),
			GetFloat(config, "grass_rock", 0.65f),
			GetBool(config, "random_use_sand", true),
			GetBool(config, "random_use_grass", true),
			GetBool(config, "random_use_rock", true),
			GetString(config, "random_sand_texture_path", TerraConfig.SandTexturePath),
			GetString(config, "random_grass_texture_path", TerraConfig.GrassTexturePath),
			GetString(config, "random_rock_texture_path", TerraConfig.RockTexturePath),
			GetInt(config, "resolution", 100),
			GetFloat(config, "water_level", 0.35f),
			GetString(config, "texture_save_path", string.Empty),
			GetBool(config, "real_map_mode", false),
			GetFloat(config, "leftup_lat", 0f),
			GetFloat(config, "leftup_lng", 0f),
			GetFloat(config, "rightdown_lat", 0f),
			GetFloat(config, "rightdown_lng", 0f),
			GetInt(config, "resolution_mode", 0),
			GetFloat(config, "realmap_water_level", 0.15f),
			GetBool(config, "realmap_use_sand", true),
			GetBool(config, "realmap_use_grass", true),
			GetBool(config, "realmap_use_rock", true),
			GetString(config, "realmap_sand_texture_path", string.Empty),
			GetString(config, "realmap_grass_texture_path", string.Empty),
			GetString(config, "realmap_rock_texture_path", string.Empty),
			GetFloat(config, "realmap_object_spacing_multiplier", 0.70f),
			GetFloat(config, "smoothing", 0.5f),
			GetInt(config, "texture_mode", 0),
			GetFloat(config, "slope_blend", 0.5f),
			GetBool(config, "generate_roads", false),
			GetString(config, "road_texture_path", string.Empty),
			GetBool(config, "generate_island", false),
			GetDictionary(config, "scatter_settings"),
			GetBool(config, "continue_generation", false),
			GetString(config, "continue_direction", "x+")
		);
	}
	
	public void Generate(
		int length, int width,
		float minHeight, float maxHeight,
		float sandGrass,
		float grassRock,
		bool randomUseSand,
		bool randomUseGrass,
		bool randomUseRock,
		string randomSandTexturePath,
		string randomGrassTexturePath,
		string randomRockTexturePath,
		int resolution,
		float waterLevel,
		string savePath,
		bool realMapMode,
		float leftuplat, float leftuplng, float rightdownlat, float rightdownlng,
		int resolutionMode = 0,
		float realMapWaterLevel = 0.15f,
		bool realMapUseSand = true,
		bool realMapUseGrass = true,
		bool realMapUseRock = true,
		string realMapSandTexturePath = "",
		string realMapGrassTexturePath = "",
		string realMapRockTexturePath = "",
		float realMapObjectSpacingMultiplier = 0.70f,
		float smoothing = 0.5f,
		int textureMode = 0,
		float slopeBlend = 0.5f,
		bool generateRoads = false,
		string roadTexturePath = "",
		bool generateIsland = false,
		Godot.Collections.Dictionary scatterSettings = null,
		bool continueGeneration = false,
		string continueDirection = "x+"
	)
	{
		_cancelRequested = false;
		EmitProgressSignal(2.0f, "Подготовка генерации...");
		GD.Print("═══════════════════════════════════════");
		GD.Print("C# Generate() вызван из GDScript!");
		GD.Print($"Параметры: length={length}, width={width}, resolution={resolution}");
		GD.Print($"realMapMode={realMapMode}, generateRoads={generateRoads}");
		GD.Print("═══════════════════════════════════════");
		if (realMapMode)
		{
			// Асинхронный вызов - запускаем в фоне
			_ = GenerateRealMapTerrainAsync(
				leftuplat, leftuplng,
				rightdownlat, rightdownlng,
				resolutionMode,
				realMapWaterLevel,
				realMapUseSand,
				realMapUseGrass,
				realMapUseRock,
				realMapSandTexturePath,
				realMapGrassTexturePath,
				realMapRockTexturePath,
				realMapObjectSpacingMultiplier
			);
			return;
		}
		else
		{
			GD.Print("Режим случайной генерации");
			_ = GenerateRandomTerrainAsync(
				length, width,
				minHeight, maxHeight,
				sandGrass, grassRock,
				randomUseSand,
				randomUseGrass,
				randomUseRock,
				randomSandTexturePath,
				randomGrassTexturePath,
				randomRockTexturePath,
				resolution,
				waterLevel,
				savePath,
				smoothing,
				textureMode,
				slopeBlend,
				generateRoads,
				roadTexturePath,
				generateIsland,
				scatterSettings,
				continueGeneration,
				continueDirection
			);
		}
	}

	public void CancelGeneration()
	{
		_cancelRequested = true;
		CallDeferred(MethodName.EmitProgressSignal, 100.0f, "Генерация остановлена пользователем");
	}

	private void EmitProgressSignal(float progress, string status)
	{
		// Emit only existing signal names to avoid editor console spam.
		if (HasSignal("progress_updated"))
			EmitSignal("progress_updated", progress, status);
		if (HasSignal("ProgressUpdated"))
			EmitSignal("ProgressUpdated", progress, status);
	}

	private bool IsGenerationCanceled()
	{
		if (!_cancelRequested)
			return false;
		GD.Print("⛔ Генерация остановлена пользователем");
		return true;
	}

	private async Task GenerateRandomTerrainAsync(
		int length, int width,
		float minHeight, float maxHeight,
		float sandGrass, float grassRock,
		bool randomUseSand,
		bool randomUseGrass,
		bool randomUseRock,
		string randomSandTexturePath,
		string randomGrassTexturePath,
		string randomRockTexturePath,
		int resolution,
		float waterLevel,
		string savePath,
		float smoothing,
		int textureMode,
		float slopeBlend,
		bool generateRoads,
		string roadTexturePath,
		bool generateIsland,
		Godot.Collections.Dictionary scatterSettings,
		bool continueGeneration,
		string continueDirection
	)
	{
		GD.Print("🚀 GenerateRandomTerrainAsync начат");

		TerrainContinuationService.ContinueContext continuation = null;
		if (IsGenerationCanceled()) return;
		if (continueGeneration)
		{
			try
			{
				continuation = TerrainContinuationService.BuildContinueContext(this, continueDirection, ContinuationDebugLogging);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"❌ Ошибка продолжения генерации: {ex.Message}");
				return;
			}
			if (continuation == null)
			{
				GD.PrintErr("❌ Не удалось продолжить генерацию: исходный мэш не найден или невалиден.");
				return;
			}

			minHeight = continuation.SourceMinHeight;
			maxHeight = continuation.SourceMaxHeight;

			if (continuation.Direction == TerrainContinuationService.ContinueDirection.XPlus || continuation.Direction == TerrainContinuationService.ContinueDirection.XMinus)
			{
				width = continuation.SourceWidth;
			}
			else
			{
				length = continuation.SourceLength;
			}

			int oldResolution = resolution;
			resolution = Mathf.Max(resolution, continuation.SuggestedResolution);
			if (ContinuationDebugLogging && resolution != oldResolution)
			{
				GD.Print($"📏 CONT resolution adjusted: requested={oldResolution}, suggested={continuation.SuggestedResolution}, final={resolution}");
			}

			if (continuation.SourceWaterY.HasValue)
			{
				float range = Mathf.Max(0.0001f, maxHeight - minHeight);
				waterLevel = Mathf.Clamp(((continuation.SourceWaterY.Value - minHeight) / range) + 0.5f, 0.0f, 1.0f);
			}
		}
		
		// Обновляем прогресс
		EmitProgressSignal(10.0f, "Генерация меша...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (IsGenerationCanceled()) return;
		
		GD.Print("📦 Создаю RandomTerrainGenerator...");
		var random = new RandomTerrainGenerator();

		float yOffset = (maxHeight - minHeight) * 0.5f;
		Vector3 plannedPosition = continuation == null
			? new Vector3(0f, yOffset, 0f)
			: TerrainContinuationService.ComputeContinuationPosition(continuation, length, width, yOffset);

		int baseSeed = GetOrCreateGeneratorSeed(MetaNoiseSeedBase, 10000);
		int hillSeed = GetOrCreateGeneratorSeed(MetaNoiseSeedHill, 11000);
		int detailSeed = GetOrCreateGeneratorSeed(MetaNoiseSeedDetail, 12000);
		int coastSeed = GetOrCreateGeneratorSeed(MetaNoiseSeedCoast, 13000);

		if (ContinuationDebugLogging)
		{
			GD.Print($"🧪 CONT noise seeds: base={baseSeed} hill={hillSeed} detail={detailSeed} coast={coastSeed}");
			GD.Print($"🧪 CONT noise sample offset: x={plannedPosition.X:F2} z={plannedPosition.Z:F2}");
		}

		GD.Print($"🔨 Генерирую меш: length={length}, width={width}, resolution={resolution}");
		Mesh mesh = random.GenerateMesh(
			length, width,
			minHeight, maxHeight,
			resolution,
			smoothing,
			generateIsland,
			waterLevel,
			baseSeed,
			hillSeed,
			detailSeed,
			coastSeed,
			plannedPosition.X,
			plannedPosition.Z
		);
		if (IsGenerationCanceled()) return;

		if (continuation != null)
		{
			TerrainContinuationService.ApplyEdgeConstraintToMesh(mesh, resolution, continuation, ContinuationDebugLogging);
		}
		
		if (mesh == null)
		{
			GD.PrintErr("❌ Меш не был создан!");
			return;
		}
		if (mesh.GetSurfaceCount() == 0)
		{
			GD.PrintErr("❌ Меш не содержит поверхностей после генерации/стыковки!");
			return;
		}
		
		GD.Print($"✅ Меш создан, поверхностей: {mesh.GetSurfaceCount()}");

		EmitProgressSignal(30.0f, "Создание экземпляра меша...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (IsGenerationCanceled()) return;

		var meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			Name = "GeneratedMesh"
		};

		meshInstance.RotateX(Mathf.Pi);
		int chunkIndex = continuation?.NextChunkIndex ?? GetNextChunkIndex();
		meshInstance.Position = plannedPosition;
		meshInstance.Name = continuation == null
			? BuildInitialMeshName(chunkIndex)
			: BuildContinuedMeshName(continuation.DirectionText, chunkIndex);
		meshInstance.SetMeta("terrain_length", length);
		meshInstance.SetMeta("terrain_width", width);
		meshInstance.SetMeta("terrain_resolution", resolution);
		meshInstance.SetMeta("terrain_min_height", minHeight);
		meshInstance.SetMeta("terrain_max_height", maxHeight);
		meshInstance.SetMeta("terrain_sand_grass", sandGrass);
		meshInstance.SetMeta("terrain_grass_rock", grassRock);
		meshInstance.SetMeta("terrain_smoothing", smoothing);
		meshInstance.SetMeta("terrain_texture_mode", textureMode);
		meshInstance.SetMeta("terrain_slope_blend", slopeBlend);

		AddChild(meshInstance);
		if (Owner != null) meshInstance.Owner = Owner;

		EmitProgressSignal(50.0f, "Применение текстур...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (IsGenerationCanceled())
		{
			meshInstance.QueueFree();
			return;
		}
		
		// Вычисляем разрешение текстуры, которое будет использоваться в TerrainTexturePainter
		// Это должно совпадать с разрешением, которое вычисляется в TerrainTexturePainter
		int maxMapSize = Mathf.Max(length, width);
		int texRes = TerraConfig.GetTextureResolutionForSize(maxMapSize);
		
		// Генерируем маску дорог, если включена опция
		float[,] roadMask = null;
		if (generateRoads)
		{
			EmitProgressSignal(55.0f, "Генерация маски дорог...");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			
			// Вычисляем пропорциональную ширину дороги
			float roadWidth = TerraConfig.GetRoadWidthForTerrain(length, width);
			
			GD.Print($"🛣️ Генерация маски дорог с разрешением: {texRes}x{texRes}");
			
			roadMask = RoadGenerator.GenerateRoadMask(
				meshInstance,
				length,
				width,
				minHeight,
				maxHeight,
				resolution,
				texRes,
				roadWidth,
				waterLevel,
				sandGrass,
				grassRock
			);
			
			if (roadMask != null)
			{
				// Проверяем, сколько пикселей в маске имеют значение > 0
				int roadPixels = 0;
				for (int x = 0; x < texRes; x++)
				{
					for (int z = 0; z < texRes; z++)
					{
						if (roadMask[x, z] > 0.0f) roadPixels++;
					}
				}
				GD.Print($"✅ Маска дорог создана: {texRes}x{texRes}, пикселей дорог: {roadPixels}");
			}
		}

		ResolveTexturePaths(
			randomUseSand,
			randomUseGrass,
			randomUseRock,
			randomSandTexturePath,
			randomGrassTexturePath,
			randomRockTexturePath,
			out string sandPath,
			out string grassPath,
			out string rockPath
		);
		
		await TerrainTexturePainter.ApplyHeightTexture(
			meshInstance,
			minHeight,
			maxHeight,
			sandPath,
			grassPath,
			rockPath,
			savePath,
			sandGrass,
			grassRock,
			length,
			width,
			textureMode,
			slopeBlend,
			roadMask,
			roadTexturePath,
			(progress, status) =>
			{
				CallDeferred(MethodName.EmitProgressSignal, progress, status);
			},
			() => _cancelRequested
		);

		if (IsGenerationCanceled())
		{
			meshInstance.QueueFree();
			return;
		}

		EmitProgressSignal(80.0f, "Создание воды...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		float worldWater = continuation?.SourceWaterY ?? (Mathf.Lerp(minHeight, maxHeight, waterLevel) - yOffset);

		var water = random.GenerateWaterPlane(length, width, worldWater);
		water.Position = new Vector3(meshInstance.Position.X, worldWater, meshInstance.Position.Z);
		water.Name = continuation == null
			? BuildInitialWaterName(chunkIndex)
			: BuildContinuedWaterName(continuation.DirectionText, chunkIndex);

		AddChild(water);
		if (Owner != null) water.Owner = Owner;

		// Дороги теперь накладываются как текстура поверх основной текстуры террейна
		// Генерация маски дорог происходит выше, перед применением текстур

		if (scatterSettings != null && scatterSettings.Count > 0)
		{
			EmitProgressSignal(92.0f, "Размещение объектов...");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			ObjectScatterPlacer.Scatter(
				this,
				meshInstance,
				length,
				width,
				resolution,
				minHeight,
				maxHeight,
				waterLevel,
				roadMask,
				texRes,
				scatterSettings,
				Owner
			);
		}

		EmitProgressSignal(100.0f, "Генерация завершена!");
	}

	private async Task GenerateRealMapTerrainAsync(
		float leftuplat, float leftuplng, float rightdownlat, float rightdownlng,
		int resolutionMode = 0,
		float realMapWaterLevel = 0.15f,
		bool realMapUseSand = true,
		bool realMapUseGrass = true,
		bool realMapUseRock = true,
		string realMapSandTexturePath = "",
		string realMapGrassTexturePath = "",
		string realMapRockTexturePath = "",
		float realMapObjectSpacingMultiplier = 0.70f
	)
	{
		// Передаем callback для обновления прогресса
		await RealMapTerrainGenerator.Generate(
			this,
			leftuplat, leftuplng,
			rightdownlat, rightdownlng,
			Owner,
			resolutionMode,
			realMapWaterLevel,
			realMapUseSand,
			realMapUseGrass,
			realMapUseRock,
			realMapSandTexturePath,
			realMapGrassTexturePath,
			realMapRockTexturePath,
			realMapObjectSpacingMultiplier,
			(progress, status) => {
				CallDeferred(MethodName.EmitProgressSignal, progress, status);
			}
		);
	}

	private static bool GetBool(Godot.Collections.Dictionary dict, string key, bool fallback)
	{
		if (dict == null || !dict.ContainsKey(key)) return fallback;
		return dict[key].AsBool();
	}

	private static int GetInt(Godot.Collections.Dictionary dict, string key, int fallback)
	{
		if (dict == null || !dict.ContainsKey(key)) return fallback;
		return dict[key].AsInt32();
	}

	private static float GetFloat(Godot.Collections.Dictionary dict, string key, float fallback)
	{
		if (dict == null || !dict.ContainsKey(key)) return fallback;
		return dict[key].AsSingle();
	}

	private static string GetString(Godot.Collections.Dictionary dict, string key, string fallback)
	{
		if (dict == null || !dict.ContainsKey(key)) return fallback;
		return dict[key].AsString();
	}

	private static Godot.Collections.Dictionary GetDictionary(Godot.Collections.Dictionary dict, string key)
	{
		if (dict == null || !dict.ContainsKey(key)) return null;
		return dict[key].VariantType == Variant.Type.Dictionary ? dict[key].AsGodotDictionary() : null;
	}

	private static void ResolveTexturePaths(
		bool useSandTexture,
		bool useGrassTexture,
		bool useRockTexture,
		string sandTexturePath,
		string grassTexturePath,
		string rockTexturePath,
		out string sandPath,
		out string grassPath,
		out string rockPath
	)
	{
		if (!(useSandTexture || useGrassTexture || useRockTexture))
			useSandTexture = true;

		string sandTex = string.IsNullOrWhiteSpace(sandTexturePath) ? TerraConfig.SandTexturePath : sandTexturePath;
		string grassTex = string.IsNullOrWhiteSpace(grassTexturePath) ? TerraConfig.GrassTexturePath : grassTexturePath;
		string rockTex = string.IsNullOrWhiteSpace(rockTexturePath) ? TerraConfig.RockTexturePath : rockTexturePath;

		if (useSandTexture && useGrassTexture && useRockTexture)
		{
			sandPath = sandTex; grassPath = grassTex; rockPath = rockTex;
		}
		else if (useSandTexture && useGrassTexture && !useRockTexture)
		{
			sandPath = sandTex; grassPath = grassTex; rockPath = grassTex;
		}
		else if (useSandTexture && !useGrassTexture && useRockTexture)
		{
			sandPath = sandTex; grassPath = rockTex; rockPath = rockTex;
		}
		else if (!useSandTexture && useGrassTexture && useRockTexture)
		{
			sandPath = grassTex; grassPath = grassTex; rockPath = rockTex;
		}
		else if (useSandTexture && !useGrassTexture && !useRockTexture)
		{
			sandPath = sandTex; grassPath = sandTex; rockPath = sandTex;
		}
		else if (!useSandTexture && useGrassTexture && !useRockTexture)
		{
			sandPath = grassTex; grassPath = grassTex; rockPath = grassTex;
		}
		else
		{
			sandPath = rockTex; grassPath = rockTex; rockPath = rockTex;
		}
	}


	private int GetNextChunkIndex()
	{
		int maxIndex = 0;
		foreach (Node child in GetChildren())
		{
			if (child is not MeshInstance3D mi) continue;
			string name = mi.Name.ToString();
			if (!name.StartsWith("GeneratedMesh_Chunk_")) continue;
			string[] parts = name.Split('_');
			if (parts.Length < 4) continue;
			if (int.TryParse(parts[3], out int idx) && idx > maxIndex)
				maxIndex = idx;
		}
		return maxIndex + 1;
	}

	private int GetOrCreateGeneratorSeed(string metaKey, int salt)
	{
		if (HasMeta(metaKey))
		{
			Variant existing = GetMeta(metaKey);
			if (existing.VariantType == Variant.Type.Int)
				return existing.AsInt32();
			if (existing.VariantType == Variant.Type.Float)
				return Mathf.RoundToInt(existing.AsSingle());
		}

		int seed = unchecked((int)GD.Randi() + salt);
		SetMeta(metaKey, seed);
		return seed;
	}

	private static string BuildInitialMeshName(int index)
	{
		return $"GeneratedMesh_Chunk_{index:0000}";
	}

	private static string BuildContinuedMeshName(string direction, int index)
	{
		return $"GeneratedMesh_Chunk_{index:0000}_{direction}";
	}

	private static string BuildInitialWaterName(int index)
	{
		return $"WaterPlane_Chunk_{index:0000}";
	}

	private static string BuildContinuedWaterName(string direction, int index)
	{
		return $"WaterPlane_Chunk_{index:0000}_{direction}";
	}
}
