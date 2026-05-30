using Godot;

// Подбирает итоговые пути текстур terrain с учётом флагов использования и custom paths.
public static class TerrainTexturePaths
{
	public static void Resolve(
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
}
