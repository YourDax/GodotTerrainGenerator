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
		float slopeBlend = 0.5f
	)
	{
		GD.Print("C# Generate() вызван из GDScript!");
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
				slopeBlend
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
		float slopeBlend
	)
	{
		// Обновляем прогресс
		EmitSignal(SignalName.ProgressUpdated, 10.0f, "Генерация меша...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		var random = new RandomTerrainGenerator();

		Mesh mesh = random.GenerateMesh(
			length, width,
			minHeight, maxHeight,
			resolution,
			smoothing
		);

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
			slopeBlend
		);

		EmitSignal(SignalName.ProgressUpdated, 80.0f, "Создание воды...");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		float worldWater = Mathf.Lerp(minHeight, maxHeight, waterLevel) - yOffset;

		var water = random.GenerateWaterPlane(length, width, worldWater);

		AddChild(water);
		if (Owner != null) water.Owner = Owner;

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
