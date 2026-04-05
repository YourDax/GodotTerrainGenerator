using Godot;

public static class TerraConfig
{
	public const string SandTexturePath = "res://addons/terragenerating/Texture/sand.png";
	public const string GrassTexturePath = "res://addons/terragenerating/Texture/grass.png";
	public const string RockTexturePath = "res://addons/terragenerating/Texture/rock.png";
	public const string DefaultRoadTexturePath = "res://addons/terragenerating/Texture/road.jpg";

	public const int OpenTopoMaxPointsPerRequest = 100;
	public const int OpenTopoMaxRetries = 5;
	public const int OpenTopoRequestDelayMs = 1000;
	public const int OpenTopoRetryDelayMs = 1000;
	public const int OpenTopoTimeoutSeconds = 4;

	public const float DefaultRoadWidthPercent = 0.02f;
	public const float MinRoadWidthWorld = 1.0f;
	public const float MaxRoadWidthWorld = 5.0f;

	public static int GetTextureResolutionForSize(int maxSize)
	{
		if (maxSize > 500) return 4096;
		if (maxSize > 300) return 3072;
		if (maxSize > 200) return 2048;
		if (maxSize > 100) return 1536;
		if (maxSize > 50) return 1280;
		return 1024;
	}

	public static float GetTileScaleForSize(int maxSize)
	{
		// Adaptive continuous formula instead of hard thresholds.
		// Fitted to keep close-up detail on large maps while avoiding over-tiling on small maps.
		// Approx targets: 50 -> ~5, 100 -> ~8, 300 -> ~15, 500 -> ~21, 1200 -> ~36.
		float size = Mathf.Max(1.0f, maxSize);
		float tileScale = 0.439f * Mathf.Pow(size, 0.621f);
		return Mathf.Clamp(tileScale, 5.0f, 60.0f);
	}

	public static float GetRoadWidthForTerrain(int length, int width)
	{
		float roadWidth = Mathf.Max(length, width) * DefaultRoadWidthPercent;
		return Mathf.Clamp(roadWidth, MinRoadWidthWorld, MaxRoadWidthWorld);
	}
}
