using Godot;

// Параметры одного запуска генерации terrain из UI или внешнего кода.
public sealed class TerraGenerationConfig
{
	public int Length { get; set; } = 100;
	public int Width { get; set; } = 100;
	public float MinHeight { get; set; } = 0f;
	public float MaxHeight { get; set; } = 25f;
	public float SandGrass { get; set; } = 0.35f;
	public float GrassRock { get; set; } = 0.65f;

	public bool RandomUseSand { get; set; } = true;
	public bool RandomUseGrass { get; set; } = true;
	public bool RandomUseRock { get; set; } = true;
	public string RandomSandTexturePath { get; set; } = TerraConfig.SandTexturePath;
	public string RandomGrassTexturePath { get; set; } = TerraConfig.GrassTexturePath;
	public string RandomRockTexturePath { get; set; } = TerraConfig.RockTexturePath;

	public int Resolution { get; set; } = 100;
	public float WaterLevel { get; set; } = 0.35f;
	public string TextureSavePath { get; set; } = string.Empty;

	public bool RealMapMode { get; set; }
	public float LeftUpLat { get; set; }
	public float LeftUpLng { get; set; }
	public float RightDownLat { get; set; }
	public float RightDownLng { get; set; }
	public int ResolutionMode { get; set; }
	public float RealMapWaterLevel { get; set; } = 0.15f;
	public bool RealMapUseSand { get; set; } = true;
	public bool RealMapUseGrass { get; set; } = true;
	public bool RealMapUseRock { get; set; } = true;
	public string RealMapSandTexturePath { get; set; } = string.Empty;
	public string RealMapGrassTexturePath { get; set; } = string.Empty;
	public string RealMapRockTexturePath { get; set; } = string.Empty;
	public float RealMapObjectSpacingMultiplier { get; set; } = 0.70f;

	public float Smoothing { get; set; } = 0.5f;
	public int TextureMode { get; set; }
	public float SlopeBlend { get; set; } = 0.5f;
	public bool GenerateRoads { get; set; }
	public string RoadTexturePath { get; set; } = string.Empty;
	public bool GenerateIsland { get; set; }
	public Godot.Collections.Dictionary ScatterSettings { get; set; }

	public bool ContinueGeneration { get; set; }
	public string ContinueDirection { get; set; } = "x+";

	public static TerraGenerationConfig FromDictionary(Godot.Collections.Dictionary config)
	{
		if (config == null)
			return null;

		return new TerraGenerationConfig
		{
			Length = GetInt(config, "length", 100),
			Width = GetInt(config, "width", 100),
			MinHeight = GetFloat(config, "min_height", 0f),
			MaxHeight = GetFloat(config, "max_height", 25f),
			SandGrass = GetFloat(config, "sand_grass", 0.35f),
			GrassRock = GetFloat(config, "grass_rock", 0.65f),
			RandomUseSand = GetBool(config, "random_use_sand", true),
			RandomUseGrass = GetBool(config, "random_use_grass", true),
			RandomUseRock = GetBool(config, "random_use_rock", true),
			RandomSandTexturePath = GetString(config, "random_sand_texture_path", TerraConfig.SandTexturePath),
			RandomGrassTexturePath = GetString(config, "random_grass_texture_path", TerraConfig.GrassTexturePath),
			RandomRockTexturePath = GetString(config, "random_rock_texture_path", TerraConfig.RockTexturePath),
			Resolution = GetInt(config, "resolution", 100),
			WaterLevel = GetFloat(config, "water_level", 0.35f),
			TextureSavePath = GetString(config, "texture_save_path", string.Empty),
			RealMapMode = GetBool(config, "real_map_mode", false),
			LeftUpLat = GetFloat(config, "leftup_lat", 0f),
			LeftUpLng = GetFloat(config, "leftup_lng", 0f),
			RightDownLat = GetFloat(config, "rightdown_lat", 0f),
			RightDownLng = GetFloat(config, "rightdown_lng", 0f),
			ResolutionMode = GetInt(config, "resolution_mode", 0),
			RealMapWaterLevel = GetFloat(config, "realmap_water_level", 0.15f),
			RealMapUseSand = GetBool(config, "realmap_use_sand", true),
			RealMapUseGrass = GetBool(config, "realmap_use_grass", true),
			RealMapUseRock = GetBool(config, "realmap_use_rock", true),
			RealMapSandTexturePath = GetString(config, "realmap_sand_texture_path", string.Empty),
			RealMapGrassTexturePath = GetString(config, "realmap_grass_texture_path", string.Empty),
			RealMapRockTexturePath = GetString(config, "realmap_rock_texture_path", string.Empty),
			RealMapObjectSpacingMultiplier = GetFloat(config, "realmap_object_spacing_multiplier", 0.70f),
			Smoothing = GetFloat(config, "smoothing", 0.5f),
			TextureMode = GetInt(config, "texture_mode", 0),
			SlopeBlend = GetFloat(config, "slope_blend", 0.5f),
			GenerateRoads = GetBool(config, "generate_roads", false),
			RoadTexturePath = GetString(config, "road_texture_path", string.Empty),
			GenerateIsland = GetBool(config, "generate_island", false),
			ScatterSettings = GetDictionary(config, "scatter_settings"),
			ContinueGeneration = GetBool(config, "continue_generation", false),
			ContinueDirection = GetString(config, "continue_direction", "x+")
		};
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
}
