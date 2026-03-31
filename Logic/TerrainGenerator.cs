using Godot;
using System;
using System.Threading.Tasks;

[Tool]
public partial class TerrainGenerator : Node3D
{
	// Сигнал для обновления прогресса
	[Signal]
	public delegate void ProgressUpdatedEventHandler(float progress, string status);
	
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
		float smoothing = 1.0f,
		int textureMode = 0,
		float slopeBlend = 0.5f,
		bool generateRoads = false,
		string roadTexturePath = "",
		bool generateIsland = false,
		Godot.Collections.Dictionary scatterSettings = null
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
				resolutionMode
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
				scatterSettings
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
		Godot.Collections.Dictionary scatterSettings
	)
	{
		GD.Print("🚀 GenerateRandomTerrainAsync начат");
		
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
		
		if (mesh == null)
		{
			GD.PrintErr("❌ Меш не был создан!");
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
		meshInstance.Position = new Vector3(0, yOffset, 0);

		AddChild(meshInstance);
		if (Owner != null) meshInstance.Owner = Owner;

		EmitSignal(SignalName.ProgressUpdated, 50.0f, "Применение текстур...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		// Вычисляем разрешение текстуры, которое будет использоваться в TerrainTexturePainter
		// Это должно совпадать с разрешением, которое вычисляется в TerrainTexturePainter
		int maxMapSize = Mathf.Max(length, width);
		int texRes = 1024; // Базовое разрешение
		if (maxMapSize > 500) texRes = 4096;
		else if (maxMapSize > 300) texRes = 3072;
		else if (maxMapSize > 200) texRes = 2048;
		else if (maxMapSize > 100) texRes = 1536;
		else if (maxMapSize > 50) texRes = 1280;
		
		// Генерируем маску дорог, если включена опция
		float[,] roadMask = null;
		if (generateRoads)
		{
			EmitSignal(SignalName.ProgressUpdated, 55.0f, "Генерация маски дорог...");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			
			// Вычисляем пропорциональную ширину дороги
			float roadWidth = Mathf.Max(length, width) * 0.02f; // 2% от максимального размера
			roadWidth = Mathf.Clamp(roadWidth, 1.0f, 5.0f); // Ограничиваем от 1 до 5 единиц
			
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
			"res://textures/sand.png",
			"res://textures/grass.png",
			"res://textures/rock.png",
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

		float worldWater = Mathf.Lerp(minHeight, maxHeight, waterLevel) - yOffset;

		var water = random.GenerateWaterPlane(length, width, worldWater);

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
		int resolutionMode = 0
	)
	{
		// Передаем callback для обновления прогресса
		await RealMapTerrainGenerator.Generate(
			this,
			leftuplat, leftuplng,
			rightdownlat, rightdownlng,
			Owner,
			resolutionMode,
			(progress, status) => {
				CallDeferred(MethodName.EmitSignal, SignalName.ProgressUpdated, progress, status);
			}
		);
	}
}
