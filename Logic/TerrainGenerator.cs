using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Tool]
public partial class TerrainGenerator : Node3D
{
	private const bool ContinuationDebugLogging = true;

	private enum ContinueDirection
	{
		XPlus,
		XMinus,
		ZPlus,
		ZMinus,
	}

	private sealed class ContinueContext
	{
		public ContinueDirection Direction;
		public string DirectionText;
		public int SourceLength;
		public int SourceWidth;
		public int SuggestedResolution;
		public float SourceMinHeight;
		public float SourceMaxHeight;
		public float? SourceWaterY;
		public float FrontierFaceCoord;
		public float AxisMin;
		public float AxisMax;
		public float AxisCenter;
		public float BaseY;
		public List<FrontierSegment> FrontierSegments;
		public int NextChunkIndex;
	}

	private sealed class FrontierSegment
	{
		public MeshInstance3D Mesh;
		public float AxisMin;
		public float AxisMax;
		public float[,] EdgeRows;
	}

	private sealed class FrontierCandidate
	{
		public MeshInstance3D Mesh;
		public int Length;
		public int Width;
		public int Resolution;
		public float MinHeight;
		public float MaxHeight;
		public float FaceCoord;
		public float AxisMin;
		public float AxisMax;
		public float Y;
	}

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
			GetFloat(config, "realmap_object_spacing_multiplier", 0.70f),
			GetFloat(config, "smoothing", 1.0f),
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
		float realMapObjectSpacingMultiplier = 0.70f,
		float smoothing = 1.0f,
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
	private async Task GenerateRandomTerrainAsync(
		int length, int width,
		float minHeight, float maxHeight,
		float sandGrass, float grassRock,
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

		ContinueContext continuation = null;
		if (continueGeneration)
		{
			try
			{
				continuation = BuildContinueContext(continueDirection);
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

			if (continuation.Direction == ContinueDirection.XPlus || continuation.Direction == ContinueDirection.XMinus)
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
		EmitSignal(SignalName.ProgressUpdated, 10.0f, "Генерация меша...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		GD.Print("📦 Создаю RandomTerrainGenerator...");
		var random = new RandomTerrainGenerator();

		GD.Print($"🔨 Генерирую меш: length={length}, width={width}, resolution={resolution}");
		Mesh mesh = random.GenerateMesh(
			length, width,
			minHeight, maxHeight,
			resolution,
			smoothing,
			generateIsland,
			waterLevel
		);

		if (continuation != null)
		{
			ApplyEdgeConstraintToMesh(mesh, resolution, continuation);
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

		EmitSignal(SignalName.ProgressUpdated, 30.0f, "Создание экземпляра меша...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		var meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			Name = "GeneratedMesh"
		};

		meshInstance.RotateX(Mathf.Pi);
		float yOffset = (maxHeight - minHeight) * 0.5f;
		int chunkIndex = continuation?.NextChunkIndex ?? GetNextChunkIndex();
		meshInstance.Position = continuation == null
			? new Vector3(0, yOffset, 0)
			: ComputeContinuationPosition(continuation, length, width, yOffset);
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

		EmitSignal(SignalName.ProgressUpdated, 50.0f, "Применение текстур...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		// Вычисляем разрешение текстуры, которое будет использоваться в TerrainTexturePainter
		// Это должно совпадать с разрешением, которое вычисляется в TerrainTexturePainter
		int maxMapSize = Mathf.Max(length, width);
		int texRes = TerraConfig.GetTextureResolutionForSize(maxMapSize);
		
		// Генерируем маску дорог, если включена опция
		float[,] roadMask = null;
		if (generateRoads)
		{
			EmitSignal(SignalName.ProgressUpdated, 55.0f, "Генерация маски дорог...");
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
		
		TerrainTexturePainter.ApplyHeightTexture(
			meshInstance,
			minHeight,
			maxHeight,
			TerraConfig.SandTexturePath,
			TerraConfig.GrassTexturePath,
			TerraConfig.RockTexturePath,
			savePath,
			sandGrass,
			grassRock,
			length,
			width,
			textureMode,
			slopeBlend,
			roadMask,
			roadTexturePath
		);

		EmitSignal(SignalName.ProgressUpdated, 80.0f, "Создание воды...");
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
			EmitSignal(SignalName.ProgressUpdated, 92.0f, "Размещение объектов...");
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

		EmitSignal(SignalName.ProgressUpdated, 100.0f, "Генерация завершена!");
	}

	private async Task GenerateRealMapTerrainAsync(
		float leftuplat, float leftuplng, float rightdownlat, float rightdownlng,
		int resolutionMode = 0,
		float realMapWaterLevel = 0.15f,
		bool realMapUseSand = true,
		bool realMapUseGrass = true,
		bool realMapUseRock = true,
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
			realMapObjectSpacingMultiplier,
			(progress, status) => {
				CallDeferred(MethodName.EmitSignal, SignalName.ProgressUpdated, progress, status);
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

	private ContinueContext BuildContinueContext(string directionText)
	{
		if (!TryParseDirection(directionText, out ContinueDirection direction))
			throw new InvalidOperationException($"Неизвестное направление continuation: {directionText}");

		List<FrontierCandidate> candidates = CollectFrontierCandidates(direction);
		if (candidates.Count == 0)
			throw new InvalidOperationException("В узле нет GeneratedMesh для продолжения.");

		float frontierFace = direction == ContinueDirection.XPlus || direction == ContinueDirection.ZPlus
			? float.NegativeInfinity
			: float.PositiveInfinity;
		for (int i = 0; i < candidates.Count; i++)
		{
			if (direction == ContinueDirection.XPlus || direction == ContinueDirection.ZPlus)
				frontierFace = Mathf.Max(frontierFace, candidates[i].FaceCoord);
			else
				frontierFace = Mathf.Min(frontierFace, candidates[i].FaceCoord);
		}

		float faceEps = 0.05f;
		var frontier = new List<FrontierCandidate>();
		for (int i = 0; i < candidates.Count; i++)
		{
			if (Mathf.Abs(candidates[i].FaceCoord - frontierFace) <= faceEps)
				frontier.Add(candidates[i]);
		}
		if (frontier.Count == 0)
			throw new InvalidOperationException("Не удалось определить фронт продолжения.");

		frontier.Sort((a, b) => a.AxisMin.CompareTo(b.AxisMin));
		ValidateFrontierContinuity(frontier);

		float axisMin = frontier[0].AxisMin;
		float axisMax = frontier[0].AxisMax;
		for (int i = 1; i < frontier.Count; i++)
		{
			axisMin = Mathf.Min(axisMin, frontier[i].AxisMin);
			axisMax = Mathf.Max(axisMax, frontier[i].AxisMax);
		}
		float axisSpan = axisMax - axisMin;
		if (axisSpan < 0.5f)
			throw new InvalidOperationException("Граница continuation слишком мала: невозможно построить корректный шов.");

		float baseY = 0f;
		for (int i = 0; i < frontier.Count; i++) baseY += frontier[i].Y;
		baseY /= frontier.Count;

		float srcMinH = float.MaxValue;
		float srcMaxH = float.MinValue;
		var segments = new List<FrontierSegment>();
		for (int i = 0; i < frontier.Count; i++)
		{
			FrontierCandidate c = frontier[i];
			float[,] h = ExtractHeightsFromMeshByUv(c.Mesh, c.Resolution);
			if (h == null)
				throw new InvalidOperationException($"Не удалось извлечь высоты из {c.Mesh.Name}.");

			float minH = c.MinHeight;
			float maxH = c.MaxHeight;
			if (maxH - minH < 0.001f)
				GetMinMax(h, out minH, out maxH);
			srcMinH = Mathf.Min(srcMinH, minH);
			srcMaxH = Mathf.Max(srcMaxH, maxH);

			segments.Add(new FrontierSegment
			{
				Mesh = c.Mesh,
				AxisMin = c.AxisMin,
				AxisMax = c.AxisMax,
				EdgeRows = BuildEdgeRowsForSegment(h, direction, 2),
			});

			if (ContinuationDebugLogging)
			{
				int samples = segments[^1].EdgeRows.GetLength(1);
				int mid = Mathf.Clamp(samples / 2, 0, Mathf.Max(0, samples - 1));
				float r0a = segments[^1].EdgeRows[0, 0];
				float r0m = segments[^1].EdgeRows[0, mid];
				float r0b = segments[^1].EdgeRows[0, samples - 1];
				GD.Print($"🧩 CONT segment [{c.Mesh.Name}] dir={directionText} axis=[{c.AxisMin:F2}..{c.AxisMax:F2}] face={c.FaceCoord:F2} res={c.Resolution} edgeRow0(start/mid/end)={r0a:F2}/{r0m:F2}/{r0b:F2}");
			}
		}

		int sourceLength = frontier[0].Length;
		int sourceWidth = frontier[0].Width;
		if (direction == ContinueDirection.XPlus || direction == ContinueDirection.XMinus)
			sourceWidth = Mathf.Max(1, Mathf.RoundToInt(axisSpan));
		else
			sourceLength = Mathf.Max(1, Mathf.RoundToInt(axisSpan));
		int suggestedResolution = EstimateContinuationResolution(frontier, sourceLength, sourceWidth, frontier[0].Resolution);

		float? sourceWaterY = FindNearestWaterY(new Vector3(
			direction == ContinueDirection.ZPlus || direction == ContinueDirection.ZMinus ? (axisMin + axisMax) * 0.5f : frontier[0].Mesh.Position.X,
			baseY,
			direction == ContinueDirection.XPlus || direction == ContinueDirection.XMinus ? (axisMin + axisMax) * 0.5f : frontier[0].Mesh.Position.Z
		));

		if (ContinuationDebugLogging)
		{
			GD.Print($"🧭 CONT context dir={directionText} frontierFace={frontierFace:F2} axis=[{axisMin:F2}..{axisMax:F2}] span={axisSpan:F2} sourceLen={sourceLength} sourceWid={sourceWidth} suggestedRes={suggestedResolution} baseY={baseY:F2} frontierCount={frontier.Count}");
		}

		return new ContinueContext
		{
			Direction = direction,
			DirectionText = directionText,
			SourceLength = sourceLength,
			SourceWidth = sourceWidth,
			SuggestedResolution = suggestedResolution,
			SourceMinHeight = srcMinH,
			SourceMaxHeight = srcMaxH,
			SourceWaterY = sourceWaterY,
			FrontierFaceCoord = frontierFace,
			AxisMin = axisMin,
			AxisMax = axisMax,
			AxisCenter = (axisMin + axisMax) * 0.5f,
			BaseY = baseY,
			FrontierSegments = segments,
			NextChunkIndex = GetNextChunkIndex(),
		};
	}

	private static int EstimateContinuationResolution(List<FrontierCandidate> frontier, int targetLength, int targetWidth, int fallbackResolution)
	{
		if (frontier == null || frontier.Count == 0)
			return Mathf.Clamp(fallbackResolution, 4, 1024);

		float sumDensityX = 0f;
		float sumDensityZ = 0f;
		int count = 0;
		for (int i = 0; i < frontier.Count; i++)
		{
			FrontierCandidate c = frontier[i];
			if (c.Resolution < 4 || c.Length <= 0 || c.Width <= 0)
				continue;

			float cells = c.Resolution - 1;
			sumDensityX += cells / c.Length;
			sumDensityZ += cells / c.Width;
			count++;
		}

		if (count == 0)
			return Mathf.Clamp(fallbackResolution, 4, 1024);

		float densityX = sumDensityX / count;
		float densityZ = sumDensityZ / count;
		int resByLength = Mathf.RoundToInt(targetLength * densityX) + 1;
		int resByWidth = Mathf.RoundToInt(targetWidth * densityZ) + 1;
		int suggested = Mathf.Max(4, Mathf.Max(resByLength, resByWidth));
		return Mathf.Clamp(suggested, 4, 1024);
	}

	private List<FrontierCandidate> CollectFrontierCandidates(ContinueDirection direction)
	{
		var outList = new List<FrontierCandidate>();
		foreach (Node child in GetChildren())
		{
			if (child is not MeshInstance3D mesh) continue;
			if (!mesh.Name.ToString().StartsWith("GeneratedMesh")) continue;

			int len = GetMeshMetaInt(mesh, "terrain_length", Mathf.RoundToInt(mesh.GetAabb().Size.X));
			int wid = GetMeshMetaInt(mesh, "terrain_width", Mathf.RoundToInt(mesh.GetAabb().Size.Z));
			int res = GetMeshMetaInt(mesh, "terrain_resolution", -1);
			if (res < 4) res = GuessResolutionFromMesh(mesh);
			res = Mathf.Max(4, res);

			float minH = mesh.HasMeta("terrain_min_height") ? mesh.GetMeta("terrain_min_height").AsSingle() : 0f;
			float maxH = mesh.HasMeta("terrain_max_height") ? mesh.GetMeta("terrain_max_height").AsSingle() : 0f;

			float face;
			float axisMin;
			float axisMax;
			switch (direction)
			{
				case ContinueDirection.XPlus:
					face = mesh.Position.X + (len * 0.5f);
					axisMin = mesh.Position.Z - (wid * 0.5f);
					axisMax = mesh.Position.Z + (wid * 0.5f);
					break;
				case ContinueDirection.XMinus:
					face = mesh.Position.X - (len * 0.5f);
					axisMin = mesh.Position.Z - (wid * 0.5f);
					axisMax = mesh.Position.Z + (wid * 0.5f);
					break;
				case ContinueDirection.ZPlus:
					face = mesh.Position.Z + (wid * 0.5f);
					axisMin = mesh.Position.X - (len * 0.5f);
					axisMax = mesh.Position.X + (len * 0.5f);
					break;
				default:
					face = mesh.Position.Z - (wid * 0.5f);
					axisMin = mesh.Position.X - (len * 0.5f);
					axisMax = mesh.Position.X + (len * 0.5f);
					break;
			}

			outList.Add(new FrontierCandidate
			{
				Mesh = mesh,
				Length = len,
				Width = wid,
				Resolution = res,
				MinHeight = minH,
				MaxHeight = maxH,
				FaceCoord = face,
				AxisMin = axisMin,
				AxisMax = axisMax,
				Y = mesh.Position.Y,
			});
		}
		return outList;
	}

	private static void ValidateFrontierContinuity(List<FrontierCandidate> frontier)
	{
		if (frontier.Count == 0)
			throw new InvalidOperationException("Frontier пуст.");
		float allowedGap = 0.35f;
		for (int i = 1; i < frontier.Count; i++)
		{
			float gap = frontier[i].AxisMin - frontier[i - 1].AxisMax;
			if (gap > allowedGap)
				throw new InvalidOperationException($"Граница continuation имеет разрыв {gap:F2} между {frontier[i - 1].Mesh.Name} и {frontier[i].Mesh.Name}.");
		}
	}

	private MeshInstance3D FindSourceMeshForDirection(ContinueDirection direction)
	{
		MeshInstance3D best = null;
		float bestMetric = direction == ContinueDirection.XPlus || direction == ContinueDirection.ZPlus
			? float.NegativeInfinity
			: float.PositiveInfinity;

		foreach (Node child in GetChildren())
		{
			if (child is not MeshInstance3D mesh)
				continue;
			if (!mesh.Name.ToString().StartsWith("GeneratedMesh"))
				continue;

			float metric = direction switch
			{
				ContinueDirection.XPlus => mesh.Position.X,
				ContinueDirection.XMinus => mesh.Position.X,
				ContinueDirection.ZPlus => mesh.Position.Z,
				_ => mesh.Position.Z,
			};

			if (best == null)
			{
				best = mesh;
				bestMetric = metric;
				continue;
			}

			bool pick = direction == ContinueDirection.XPlus || direction == ContinueDirection.ZPlus
				? metric > bestMetric
				: metric < bestMetric;
			if (pick)
			{
				best = mesh;
				bestMetric = metric;
			}
		}

		return best;
	}

	private static bool TryParseDirection(string directionText, out ContinueDirection direction)
	{
		direction = ContinueDirection.XPlus;
		if (string.IsNullOrEmpty(directionText)) return false;
		switch (directionText)
		{
			case "x+": direction = ContinueDirection.XPlus; return true;
			case "x-": direction = ContinueDirection.XMinus; return true;
			case "z+": direction = ContinueDirection.ZPlus; return true;
			case "z-": direction = ContinueDirection.ZMinus; return true;
			default: return false;
		}
	}

	private static int GuessResolutionFromMesh(MeshInstance3D mesh)
	{
		if (mesh?.Mesh == null || mesh.Mesh.GetSurfaceCount() == 0)
			return 100;
		if (mesh.Mesh is not ArrayMesh arr)
			return 100;
		var arrays = arr.SurfaceGetArrays(0);
		var vertices = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];
		if (vertices == null || vertices.Count == 0)
			return 100;

		int vc = vertices.Count;
		// Indexed grid candidate: vc ~= r*r
		int candIndexed = Mathf.Max(4, Mathf.RoundToInt(Mathf.Sqrt(vc)));
		int errIndexed = Mathf.Abs(candIndexed * candIndexed - vc);

		// Non-indexed triangles candidate: vc ~= 6*(r-1)^2
		int candTri = Mathf.Max(4, Mathf.RoundToInt(Mathf.Sqrt(vc / 6.0f) + 1.0f));
		int triV = 6 * (candTri - 1) * (candTri - 1);
		int errTri = Mathf.Abs(triV - vc);

		int best = errTri <= errIndexed ? candTri : candIndexed;
		best = Mathf.Clamp(best, 4, 1024);

		// Optional UV refinement if available and stable.
		var uvs = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.TexUV];
		if (uvs == null || uvs.Count == 0)
			return best;
		var uniqueU = new HashSet<int>();
		var uniqueV = new HashSet<int>();
		for (int i = 0; i < uvs.Count; i++)
		{
			Vector2 uv = (Vector2)uvs[i];
			// Coarse quantization to avoid tiny float noise exploding unique counts.
			uniqueU.Add(Mathf.RoundToInt(uv.X * 2000f));
			uniqueV.Add(Mathf.RoundToInt(uv.Y * 2000f));
		}

		int ru = uniqueU.Count;
		int rv = uniqueV.Count;
		if (ru >= 4 && rv >= 4)
		{
			int uvRes = Mathf.Clamp(Mathf.Min(ru, rv), 4, 1024);
			if (Mathf.Abs(uvRes - best) <= 8)
				return uvRes;
		}

		return best;
	}

	private static int GetMeshMetaInt(MeshInstance3D mesh, string key, int fallback)
	{
		if (mesh == null || !mesh.HasMeta(key)) return fallback;
		Variant v = mesh.GetMeta(key);
		if (v.VariantType == Variant.Type.Int) return v.AsInt32();
		if (v.VariantType == Variant.Type.Float) return Mathf.RoundToInt(v.AsSingle());
		return fallback;
	}

	private float? FindNearestWaterY(Vector3 around)
	{
		MeshInstance3D best = null;
		float bestDist = float.MaxValue;
		foreach (Node child in GetChildren())
		{
			if (child is not MeshInstance3D mi) continue;
			if (!mi.Name.ToString().StartsWith("WaterPlane")) continue;
			float d = around.DistanceSquaredTo(mi.Position);
			if (d < bestDist)
			{
				best = mi;
				bestDist = d;
			}
		}
		return best?.Position.Y;
	}

	private static float[,] ExtractHeightsFromMeshByUv(MeshInstance3D meshInstance, int resolution)
	{
		if (meshInstance?.Mesh == null || meshInstance.Mesh.GetSurfaceCount() == 0)
			return null;
		if (meshInstance.Mesh is not ArrayMesh arrayMesh)
			return null;

		var arrays = arrayMesh.SurfaceGetArrays(0);
		var verticesArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];
		var uvArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.TexUV];
		if (verticesArray == null || uvArray == null || verticesArray.Count == 0)
			return null;

		float[,] heights = new float[resolution, resolution];
		bool[,] filled = new bool[resolution, resolution];

		// Detect whether UV axes are aligned or flipped relative to mesh X/Z.
		// This avoids seam inversion when meshes use opposite V orientation.
		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minZ = float.MaxValue;
		float maxZ = float.MinValue;
		float uvXAtMinX = 0f;
		float uvXAtMaxX = 1f;
		float uvYAtMinZ = 0f;
		float uvYAtMaxZ = 1f;

		for (int i = 0; i < verticesArray.Count; i++)
		{
			Vector3 vert = (Vector3)verticesArray[i];
			Vector2 uv = (Vector2)uvArray[i];
			if (vert.X < minX)
			{
				minX = vert.X;
				uvXAtMinX = uv.X;
			}
			if (vert.X > maxX)
			{
				maxX = vert.X;
				uvXAtMaxX = uv.X;
			}
			if (vert.Z < minZ)
			{
				minZ = vert.Z;
				uvYAtMinZ = uv.Y;
			}
			if (vert.Z > maxZ)
			{
				maxZ = vert.Z;
				uvYAtMaxZ = uv.Y;
			}
		}

		bool flipX = uvXAtMaxX < uvXAtMinX;
		bool flipZ = uvYAtMaxZ < uvYAtMinZ;

		if (ContinuationDebugLogging)
		{
			GD.Print($"📐 CONT uv-map [{meshInstance.Name}] res={resolution} flipX={flipX} flipZ={flipZ} uvX(minX/maxX)={uvXAtMinX:F3}/{uvXAtMaxX:F3} uvY(minZ/maxZ)={uvYAtMinZ:F3}/{uvYAtMaxZ:F3}");
		}

		for (int i = 0; i < verticesArray.Count; i++)
		{
			Vector3 vert = (Vector3)verticesArray[i];
			Vector2 uv = (Vector2)uvArray[i];
			float u = flipX ? (1f - uv.X) : uv.X;
			float v = flipZ ? (1f - uv.Y) : uv.Y;
			int x = Mathf.Clamp(Mathf.RoundToInt(u * (resolution - 1)), 0, resolution - 1);
			int z = Mathf.Clamp(Mathf.RoundToInt(v * (resolution - 1)), 0, resolution - 1);
			heights[x, z] = vert.Y;
			filled[x, z] = true;
		}

		for (int z = 0; z < resolution; z++)
		{
			for (int x = 0; x < resolution; x++)
			{
				if (filled[x, z]) continue;
				float sum = 0f;
				int count = 0;
				for (int dz = -1; dz <= 1; dz++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						int nx = x + dx;
						int nz = z + dz;
						if (nx >= 0 && nx < resolution && nz >= 0 && nz < resolution && filled[nx, nz])
						{
							sum += heights[nx, nz];
							count++;
						}
					}
				}
				if (count > 0)
				{
					heights[x, z] = sum / count;
					filled[x, z] = true;
				}
			}
		}

		return heights;
	}

	private static void ApplyEdgeConstraintToMesh(Mesh mesh, int resolution, ContinueContext ctx)
	{
		if (mesh == null || ctx == null || ctx.FrontierSegments == null || ctx.FrontierSegments.Count == 0)
			throw new InvalidOperationException("Нет данных frontier для стыковки continuation.");
		if (mesh is not ArrayMesh arrayMesh || arrayMesh.GetSurfaceCount() == 0) return;

		var arrays = arrayMesh.SurfaceGetArrays(0);
		Vector3[] verticesArray = (Vector3[])arrays[(int)ArrayMesh.ArrayType.Vertex];
		Vector2[] uvArray = (Vector2[])arrays[(int)ArrayMesh.ArrayType.TexUV];
		if (verticesArray.Length == 0 || uvArray.Length == 0) return;

		int lockRows = 1;
		int blendRows = Mathf.Clamp(Mathf.RoundToInt(resolution * 0.12f), 6, 20);
		float[,] edgeStrip = BuildCombinedEdgeStrip(ctx, resolution, lockRows);
		if (ContinuationDebugLogging)
		{
			int mid = Mathf.Clamp((resolution - 1) / 2, 0, resolution - 1);
			GD.Print($"🪡 CONT seam dir={ctx.DirectionText} lockRows={lockRows} blendRows={blendRows} strip[r0]=[{edgeStrip[0, 0]:F2},{edgeStrip[0, mid]:F2},{edgeStrip[0, resolution - 1]:F2}]");
		}

		float[] seamSourceSum = new float[resolution];
		int[] seamSourceCount = new int[resolution];
		for (int i = 0; i < verticesArray.Length; i++)
		{
			Vector3 v = verticesArray[i];
			Vector2 uv = uvArray[i];
			int xi = Mathf.Clamp(Mathf.RoundToInt(uv.X * (resolution - 1)), 0, resolution - 1);
			int zi = Mathf.Clamp(Mathf.RoundToInt(uv.Y * (resolution - 1)), 0, resolution - 1);

			int dist = GetDistanceFromSeam(ctx.Direction, xi, zi, resolution);
			if (dist != 0)
				continue;

			int axis = GetAxisIndexAlongSeam(ctx.Direction, xi, zi, resolution);
			axis = Mathf.Clamp(axis, 0, resolution - 1);
			seamSourceSum[axis] += v.Y;
			seamSourceCount[axis] += 1;
		}

		float[] seamOffset = new float[resolution];
		bool[] hasOffset = new bool[resolution];
		for (int a = 0; a < resolution; a++)
		{
			if (seamSourceCount[a] <= 0)
				continue;
			float seamSource = seamSourceSum[a] / seamSourceCount[a];
			seamOffset[a] = edgeStrip[0, a] - seamSource;
			hasOffset[a] = true;
		}

		for (int a = 0; a < resolution; a++)
		{
			if (hasOffset[a])
				continue;

			int left = a - 1;
			while (left >= 0 && !hasOffset[left]) left--;
			int right = a + 1;
			while (right < resolution && !hasOffset[right]) right++;

			if (left >= 0 && right < resolution)
			{
				float tFill = (float)(a - left) / (right - left);
				seamOffset[a] = Mathf.Lerp(seamOffset[left], seamOffset[right], tFill);
				hasOffset[a] = true;
			}
			else if (left >= 0)
			{
				seamOffset[a] = seamOffset[left];
				hasOffset[a] = true;
			}
			else if (right < resolution)
			{
				seamOffset[a] = seamOffset[right];
				hasOffset[a] = true;
			}
		}

		for (int i = 0; i < verticesArray.Length; i++)
		{
			Vector3 v = verticesArray[i];
			Vector2 uv = uvArray[i];
			int xi = Mathf.Clamp(Mathf.RoundToInt(uv.X * (resolution - 1)), 0, resolution - 1);
			int zi = Mathf.Clamp(Mathf.RoundToInt(uv.Y * (resolution - 1)), 0, resolution - 1);

			int dist = GetDistanceFromSeam(ctx.Direction, xi, zi, resolution);
			int axis = GetAxisIndexAlongSeam(ctx.Direction, xi, zi, resolution);
			axis = Mathf.Clamp(axis, 0, resolution - 1);

			if (dist < lockRows)
			{
				v.Y = edgeStrip[0, axis];
				verticesArray[i] = v;
				continue;
			}

			if (dist > blendRows)
				continue;

			float t = 1f - (dist / (blendRows + 1f));
			t = Mathf.SmoothStep(0f, 1f, t);
			v.Y += seamOffset[axis] * t;
			verticesArray[i] = v;
		}

		arrays[(int)ArrayMesh.ArrayType.Vertex] = verticesArray;
		arrayMesh.ClearSurfaces();
		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		if (ContinuationDebugLogging)
		{
			GD.Print($"✅ CONT seam applied dir={ctx.DirectionText} resolution={resolution} vertices={verticesArray.Length}");
		}
	}

	private static float[,] BuildCombinedEdgeStrip(ContinueContext ctx, int targetResolution, int rows)
	{
		if (targetResolution < 2)
			throw new InvalidOperationException("targetResolution слишком мал для BuildCombinedEdgeStrip.");

		float[,] strip = new float[rows, targetResolution];
		for (int i = 0; i < targetResolution; i++)
		{
			float tAxis = (float)i / (targetResolution - 1);
			float worldAxis = Mathf.Lerp(ctx.AxisMin, ctx.AxisMax, tAxis);

			FrontierSegment segment = FindSegmentForAxis(ctx.FrontierSegments, worldAxis);
			if (segment == null)
				throw new InvalidOperationException($"Нет сегмента frontier для координаты {worldAxis:F2}.");

			float segSpan = Mathf.Max(0.0001f, segment.AxisMax - segment.AxisMin);
			float segT = Mathf.Clamp((worldAxis - segment.AxisMin) / segSpan, 0f, 1f);
			int segSamples = segment.EdgeRows.GetLength(1);
			float segIdx = segT * (segSamples - 1);

			for (int r = 0; r < rows; r++)
			{
				strip[r, i] = SampleEdgeRow(segment.EdgeRows, r, segIdx);
			}

			if (ContinuationDebugLogging && (i == 0 || i == targetResolution / 2 || i == targetResolution - 1))
			{
				GD.Print($"🔎 CONT strip-map i={i} axisWorld={worldAxis:F2} seg=[{segment.AxisMin:F2}..{segment.AxisMax:F2}] segIdx={segIdx:F2} r0={strip[0, i]:F2}");
			}
		}
		return strip;
	}

	private static FrontierSegment FindSegmentForAxis(List<FrontierSegment> segments, float axis)
	{
		FrontierSegment nearest = null;
		float nearestDist = float.MaxValue;
		for (int i = 0; i < segments.Count; i++)
		{
			FrontierSegment s = segments[i];
			if (axis >= s.AxisMin - 0.001f && axis <= s.AxisMax + 0.001f)
				return s;

			float d = axis < s.AxisMin ? (s.AxisMin - axis) : (axis - s.AxisMax);
			if (d < nearestDist)
			{
				nearestDist = d;
				nearest = s;
			}
		}

		if (nearest != null && nearestDist <= 0.35f)
			return nearest;
		return null;
	}

	private static float SampleEdgeRow(float[,] rowsData, int row, float idx)
	{
		int w = rowsData.GetLength(1);
		if (w <= 1) return rowsData[row, 0];
		idx = Mathf.Clamp(idx, 0f, w - 1);
		int i0 = Mathf.Clamp(Mathf.FloorToInt(idx), 0, w - 1);
		int i1 = Mathf.Clamp(i0 + 1, 0, w - 1);
		float t = idx - i0;
		return Mathf.Lerp(rowsData[row, i0], rowsData[row, i1], t);
	}

	private static int GetDistanceFromSeam(ContinueDirection direction, int xi, int zi, int resolution)
	{
		return direction switch
		{
			ContinueDirection.XPlus => xi,
			ContinueDirection.XMinus => (resolution - 1 - xi),
			// Mesh rotates by PI around X, so seam side for Z directions is reversed in local UV rows.
			ContinueDirection.ZPlus => (resolution - 1 - zi),
			_ => zi,
		};
	}

	private static int GetAxisIndexAlongSeam(ContinueDirection direction, int xi, int zi, int resolution)
	{
		return direction switch
		{
			// Mesh is rotated by PI around X, so local Z index grows opposite to world Z.
			ContinueDirection.XPlus => (resolution - 1 - zi),
			ContinueDirection.XMinus => (resolution - 1 - zi),
			ContinueDirection.ZPlus => xi,
			_ => xi,
		};
	}

	private static float[,] BuildEdgeRowsForSegment(float[,] sourceHeights, ContinueDirection direction, int rows)
	{
		int srcResX = sourceHeights.GetLength(0);
		int srcResZ = sourceHeights.GetLength(1);
		int samples = direction == ContinueDirection.XPlus || direction == ContinueDirection.XMinus ? srcResZ : srcResX;
		float[,] strip = new float[rows, samples];

		for (int r = 0; r < rows; r++)
		{
			for (int i = 0; i < samples; i++)
			{
				float tAxis = samples <= 1 ? 0f : (float)i / (samples - 1);
				float srcX = 0f;
				float srcZ = 0f;

				switch (direction)
				{
					case ContinueDirection.XPlus:
						srcX = (srcResX - 1) - r;
						// MeshInstance rotates by PI around X, so world axis along Z is reversed in local Z.
						srcZ = (1f - tAxis) * (srcResZ - 1);
						break;
					case ContinueDirection.XMinus:
						srcX = r;
						// MeshInstance rotates by PI around X, so world axis along Z is reversed in local Z.
						srcZ = (1f - tAxis) * (srcResZ - 1);
						break;
					case ContinueDirection.ZPlus:
						srcX = tAxis * (srcResX - 1);
						// MeshInstance rotates by PI around X, so world +Z corresponds to local min Z.
						srcZ = r;
						break;
					case ContinueDirection.ZMinus:
						srcX = tAxis * (srcResX - 1);
						// MeshInstance rotates by PI around X, so world -Z corresponds to local max Z.
						srcZ = (srcResZ - 1) - r;
						break;
				}

				strip[r, i] = TerrainMath.BilinearSample(sourceHeights, srcX, srcZ);
			}
		}

		return strip;
	}

	private static void GetMinMax(float[,] heights, out float min, out float max)
	{
		min = float.MaxValue;
		max = float.MinValue;
		for (int z = 0; z < heights.GetLength(1); z++)
		{
			for (int x = 0; x < heights.GetLength(0); x++)
			{
				float h = heights[x, z];
				if (h < min) min = h;
				if (h > max) max = h;
			}
		}
	}

	private static Vector3 ComputeContinuationPosition(ContinueContext ctx, int newLength, int newWidth, float yOffset)
	{
		float targetY = ctx.BaseY;
		return ctx.Direction switch
		{
			ContinueDirection.XPlus => new Vector3(ctx.FrontierFaceCoord + (newLength * 0.5f), targetY, ctx.AxisCenter),
			ContinueDirection.XMinus => new Vector3(ctx.FrontierFaceCoord - (newLength * 0.5f), targetY, ctx.AxisCenter),
			ContinueDirection.ZPlus => new Vector3(ctx.AxisCenter, targetY, ctx.FrontierFaceCoord + (newWidth * 0.5f)),
			_ => new Vector3(ctx.AxisCenter, targetY, ctx.FrontierFaceCoord - (newWidth * 0.5f)),
		};
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
