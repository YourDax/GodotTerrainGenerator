using Godot;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

[Tool]
// Основной узел, который запускает генерацию случайного и real-map террейна.
public partial class TerrainGenerator : Node3D
{
	private const bool ContinuationDebugLogging = true;
	private const string MetaNoiseSeedBase = "terrain_noise_seed_base";
	private const string MetaNoiseSeedHill = "terrain_noise_seed_hill";
	private const string MetaNoiseSeedDetail = "terrain_noise_seed_detail";
	private const string MetaNoiseSeedCoast = "terrain_noise_seed_coast";
	private bool _cancelRequested = false;
	private CancellationTokenSource _generationCts;

	// Сигнал для обновления прогресса
	[Signal]
	// Сообщает UI текущий процент и текст статуса генерации.
	public delegate void ProgressUpdatedEventHandler(float progress, string status);

	// Запускает генерацию из словаря конфигурации UI или внешнего кода.
	public void GenerateFromConfig(Godot.Collections.Dictionary config)
	{
		TerraGenerationConfig generationConfig = TerraGenerationConfig.FromDictionary(config);
		if (generationConfig == null)
		{
			GD.PrintErr("GenerateFromConfig: config is null");
			return;
		}

		Generate(generationConfig);
	}

	// Запускает основную генерацию террейна в случайном или real-map режиме.
	public void Generate(TerraGenerationConfig config)
	{
		if (config == null)
		{
			GD.PrintErr("Generate: config is null");
			return;
		}

		_cancelRequested = false;
		_generationCts?.Cancel();
		_generationCts?.Dispose();
		_generationCts = new CancellationTokenSource();

		Stopwatch fullGenerationStopwatch = Stopwatch.StartNew();
		EmitProgressSignal(2.0f, "Подготовка генерации...");
		GD.Print("═══════════════════════════════════════");
		GD.Print("C# Generate() вызван из GDScript!");
		GD.Print($"Параметры: length={config.Length}, width={config.Width}, resolution={config.Resolution}");
		GD.Print($"realMapMode={config.RealMapMode}, generateRoads={config.GenerateRoads}");
		GD.Print("═══════════════════════════════════════");

		if (config.RealMapMode)
		{
			_ = GenerateRealMapTerrainAsync(config, fullGenerationStopwatch, _generationCts.Token);
			return;
		}

		GD.Print("Режим случайной генерации");
		_ = GenerateRandomTerrainAsync(config, fullGenerationStopwatch);
	}

	// Помечает текущую генерацию как отменённую и уведомляет UI.
	public void CancelGeneration()
	{
		_cancelRequested = true;
		_generationCts?.Cancel();
		CallDeferred(MethodName.EmitProgressSignal, 100.0f, "Генерация остановлена пользователем");
	}

	// Отправляет сигнал прогресса в совместимом с GDScript формате.
	private void EmitProgressSignal(float progress, string status)
	{
		// Emit only existing signal names to avoid editor console spam.
		if (HasSignal("progress_updated"))
			EmitSignal("progress_updated", progress, status);
		if (HasSignal("ProgressUpdated"))
			EmitSignal("ProgressUpdated", progress, status);
	}

	// Проверяет, была ли генерация отменена пользователем.
	private bool IsGenerationCanceled()
	{
		if (!_cancelRequested)
			return false;
		GD.Print("Генерация остановлена пользователем");
		return true;
	}

	// Логирует общее время генерации и итоговый статус.
	private static void LogFullGenerationDuration(Stopwatch stopwatch, string mode, string outcome)
	{
		if (stopwatch == null)
			return;

		if (stopwatch.IsRunning)
			stopwatch.Stop();

		GD.Print($"Полный цикл генерации ({mode}) {outcome}: {stopwatch.Elapsed.TotalMilliseconds:F0} мс ({stopwatch.Elapsed.TotalSeconds:F2} с)");
	}

	// Выполняет асинхронную генерацию случайного террейна, воды, текстур и объектов.
	private async Task GenerateRandomTerrainAsync(TerraGenerationConfig config, Stopwatch fullGenerationStopwatch)
	{
		string generationOutcome = "успешно завершен";
		try
		{
			GD.Print("GenerateRandomTerrainAsync начат");

			int length = config.Length;
			int width = config.Width;
			const float minHeight = 0f;
			float maxHeight = config.MaxHeight;
			float sandGrass = config.SandGrass;
			float grassRock = config.GrassRock;
			int resolution = config.Resolution;
			float waterLevel = config.WaterLevel;
			int textureMode = config.TextureMode;
			float slopeBlend = config.SlopeBlend;

			TerrainContinuationService.ContinueContext continuation = null;
			if (IsGenerationCanceled())
			{
				generationOutcome = "остановлен пользователем";
				return;
			}
			if (config.ContinueGeneration)
			{
				try
				{
					continuation = TerrainContinuationService.BuildContinueContext(this, config.ContinueDirection, ContinuationDebugLogging);
				}
				catch (Exception ex)
				{
					generationOutcome = "завершился с ошибкой";
					GD.PrintErr($"Ошибка продолжения генерации: {ex.Message}");
					return;
				}
				if (continuation == null)
				{
					generationOutcome = "завершился с ошибкой";
					GD.PrintErr("Не удалось продолжить генерацию: исходный мэш не найден или невалиден.");
					return;
				}

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
					GD.Print($"CONT resolution adjusted: requested={oldResolution}, suggested={continuation.SuggestedResolution}, final={resolution}");
				}
			}

			EmitProgressSignal(10.0f, "Генерация меша...");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (IsGenerationCanceled())
			{
				generationOutcome = "остановлен пользователем";
				return;
			}

			GD.Print("Создаю RandomTerrainGenerator...");
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
				GD.Print($"CONT noise seeds: base={baseSeed} hill={hillSeed} detail={detailSeed} coast={coastSeed}");
				GD.Print($"CONT noise sample offset: x={plannedPosition.X:F2} z={plannedPosition.Z:F2}");
			}

			GD.Print($"Генерирую меш: length={length}, width={width}, resolution={resolution}");
			Stopwatch meshBuildStopwatch = Stopwatch.StartNew();
			Mesh mesh = random.GenerateMesh(
				length, width,
				minHeight, maxHeight,
				resolution,
				config.Smoothing,
				config.GenerateIsland,
				waterLevel,
				baseSeed,
				hillSeed,
				detailSeed,
				coastSeed,
				plannedPosition.X,
				plannedPosition.Z
			);
			meshBuildStopwatch.Stop();
			GD.Print($"Генерация меша завершена за {meshBuildStopwatch.Elapsed.TotalMilliseconds:F0} мс ({meshBuildStopwatch.Elapsed.TotalSeconds:F2} с)");
			if (IsGenerationCanceled())
			{
				generationOutcome = "остановлен пользователем";
				return;
			}

			if (continuation != null)
			{
				TerrainContinuationService.ApplyEdgeConstraintToMesh(mesh, resolution, continuation, yOffset, ContinuationDebugLogging);
			}

			if (mesh == null)
			{
				generationOutcome = "завершился с ошибкой";
				GD.PrintErr("Меш не был создан!");
				return;
			}
			if (mesh.GetSurfaceCount() == 0)
			{
				generationOutcome = "завершился с ошибкой";
				GD.PrintErr("Меш не содержит поверхностей после генерации/стыковки!");
				return;
			}

			GD.Print($"Меш создан, поверхностей: {mesh.GetSurfaceCount()}");

			EmitProgressSignal(30.0f, "Создание экземпляра меша...");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (IsGenerationCanceled())
			{
				generationOutcome = "остановлен пользователем";
				return;
			}

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
			meshInstance.SetMeta("terrain_smoothing", config.Smoothing);
			meshInstance.SetMeta("terrain_texture_mode", textureMode);
			meshInstance.SetMeta("terrain_slope_blend", slopeBlend);

			AddChild(meshInstance);
			if (Owner != null) meshInstance.Owner = Owner;

			EmitProgressSignal(50.0f, "Применение текстур...");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (IsGenerationCanceled())
			{
				generationOutcome = "остановлен пользователем";
				meshInstance.QueueFree();
				return;
			}

			int maxMapSize = Mathf.Max(length, width);
			int texRes = TerraConfig.GetTextureResolutionForSize(maxMapSize);

			float textureRefSourceMax = continuation?.SourceMaxHeight ?? 0f;
			float textureRefBaseY = continuation?.BaseY ?? 0f;
			float worldWater = continuation?.SourceWaterY ?? (Mathf.Lerp(minHeight, maxHeight, waterLevel) - yOffset);

			float[,] roadMask = null;
			if (config.GenerateRoads)
			{
				EmitProgressSignal(55.0f, "Генерация маски дорог...");
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

				float roadWidth = TerraConfig.GetRoadWidthForTerrain(length, width);

				GD.Print($"Генерация маски дорог с разрешением: {texRes}x{texRes}");

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
					grassRock,
					textureRefSourceMax,
					textureRefBaseY,
					continuation != null ? worldWater : null
				);

				if (roadMask != null)
				{
					int roadPixels = 0;
					for (int x = 0; x < texRes; x++)
					{
						for (int z = 0; z < texRes; z++)
						{
							if (roadMask[x, z] > 0.0f) roadPixels++;
						}
					}
					GD.Print($"Маска дорог создана: {texRes}x{texRes}, пикселей дорог: {roadPixels}");
				}
			}

			TerrainTexturePaths.Resolve(
				config.RandomUseSand,
				config.RandomUseGrass,
				config.RandomUseRock,
				config.RandomSandTexturePath,
				config.RandomGrassTexturePath,
				config.RandomRockTexturePath,
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
				config.TextureSavePath,
				sandGrass,
				grassRock,
				length,
				width,
				textureMode,
				slopeBlend,
				roadMask,
				config.RoadTexturePath,
				(progress, status) =>
				{
					CallDeferred(MethodName.EmitProgressSignal, progress, status);
				},
				() => _cancelRequested,
				true,
				textureRefSourceMax,
				textureRefBaseY
			);

			if (IsGenerationCanceled())
			{
				generationOutcome = "остановлен пользователем";
				meshInstance.QueueFree();
				return;
			}

			EmitProgressSignal(80.0f, "Создание воды...");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			var water = random.GenerateWaterPlane(length, width, worldWater);
			water.Position = new Vector3(meshInstance.Position.X, worldWater, meshInstance.Position.Z);
			water.Name = continuation == null
				? BuildInitialWaterName(chunkIndex)
				: BuildContinuedWaterName(continuation.DirectionText, chunkIndex);

			AddChild(water);
			if (Owner != null) water.Owner = Owner;

			if (config.ScatterSettings != null && config.ScatterSettings.Count > 0)
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
					config.ScatterSettings,
					Owner
				);
			}

			EmitProgressSignal(100.0f, "Генерация завершена!");
		}
		catch (Exception ex)
		{
			generationOutcome = "завершился с ошибкой";
			GD.PrintErr($"Ошибка полной генерации random terrain: {ex.Message}");
		}
		finally
		{
			LogFullGenerationDuration(fullGenerationStopwatch, "random", generationOutcome);
		}
	}

	// Выполняет асинхронную генерацию террейна по real-map данным.
	private async Task GenerateRealMapTerrainAsync(
		TerraGenerationConfig config,
		Stopwatch fullGenerationStopwatch,
		CancellationToken cancellationToken
	)
	{
		string generationOutcome = "успешно завершен";
		try
		{
			Node3D generated = await RealMapTerrainGenerator.Generate(
				this,
				config.LeftUpLat, config.LeftUpLng,
				config.RightDownLat, config.RightDownLng,
				Owner,
				config.ResolutionMode,
				config.RealMapWaterLevel,
				config.RealMapUseSand,
				config.RealMapUseGrass,
				config.RealMapUseRock,
				config.RealMapSandTexturePath,
				config.RealMapGrassTexturePath,
				config.RealMapRockTexturePath,
				config.RealMapObjectSpacingMultiplier,
				(progress, status) =>
				{
					CallDeferred(MethodName.EmitProgressSignal, progress, status);
				},
				() => _cancelRequested,
				cancellationToken
			);

			if (IsGenerationCanceled())
				generationOutcome = "остановлен пользователем";
			else if (generated == null)
				generationOutcome = "завершился с ошибкой";
		}
		catch (OperationCanceledException)
		{
			generationOutcome = "остановлен пользователем";
			GD.Print("Real-map генерация отменена");
		}
		catch (Exception ex)
		{
			generationOutcome = "завершился с ошибкой";
			GD.PrintErr($"Ошибка полной генерации real-map terrain: {ex.Message}");
		}
		finally
		{
			LogFullGenerationDuration(fullGenerationStopwatch, "real-map", generationOutcome);
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

	// Считывает seed из метаданных или создаёт новый, если его ещё нет.
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

	// Формирует имя для первого чанка террейна.
	private static string BuildInitialMeshName(int index)
	{
		return $"GeneratedMesh_Chunk_{index:0000}";
	}

	// Формирует имя для продолженного чанка террейна.
	private static string BuildContinuedMeshName(string direction, int index)
	{
		return $"GeneratedMesh_Chunk_{index:0000}_{direction}";
	}

	// Формирует имя для первого water plane.
	private static string BuildInitialWaterName(int index)
	{
		return $"WaterPlane_Chunk_{index:0000}";
	}

	// Формирует имя для water plane у продолженного чанка.
	private static string BuildContinuedWaterName(string direction, int index)
	{
		return $"WaterPlane_Chunk_{index:0000}_{direction}";
	}
}
