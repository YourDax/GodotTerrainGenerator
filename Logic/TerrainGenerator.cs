using Godot;
using System;
using System.Threading.Tasks;

[Tool]
public partial class TerrainGenerator : Node3D
{
	public void Generate(
		int length, int width,
		float minHeight, float maxHeight,
		float sandGrass,
		float grassRock,
		int resolution,
		float waterLevel,
		string savePath,
		bool realMapMode,
		float leftuplat, float leftuplng, float rightdownlat, float rightdownlng
	)
	{
		GD.Print("C# Generate() вызван из GDScript!");
		if (realMapMode)
		{
			// Асинхронный вызов - запускаем в фоне
			_ = GenerateRealMapTerrainAsync(
				leftuplat, leftuplng,
				rightdownlat, rightdownlng
			);
			return;
		}
		else
		{
			GD.Print("Режим случайной генерации");
			GenerateRandomTerrain(
				length, width,
				minHeight, maxHeight,
				sandGrass, grassRock,
				resolution,
				waterLevel,
				savePath
			);
		}
	}
	private void GenerateRandomTerrain(
		int length, int width,
		float minHeight, float maxHeight,
		float sandGrass, float grassRock,
		int resolution,
		float waterLevel,
		string savePath
	)
	{
		var random = new RandomTerrainGenerator();

		Mesh mesh = random.GenerateMesh(
			length, width,
			minHeight, maxHeight,
			resolution
		);

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

		TerrainTexturePainter.ApplyHeightTexture(
			meshInstance,
			minHeight,
			maxHeight,
			"res://textures/sand.png",
			"res://textures/grass.png",
			"res://textures/rock.png",
			savePath,
			sandGrass,
			grassRock
		);

		float worldWater = Mathf.Lerp(minHeight, maxHeight, waterLevel) - yOffset;

		var water = random.GenerateWaterPlane(length, width, worldWater);

		AddChild(water);
		if (Owner != null) water.Owner = Owner;
	}

	private async Task GenerateRealMapTerrainAsync(
		float leftuplat, float leftuplng, float rightdownlat, float rightdownlng
	)
	{
		await RealMapTerrainGenerator.Generate(
			this,
			leftuplat, leftuplng,
			rightdownlat, rightdownlng,
			Owner
		);
	}
}
