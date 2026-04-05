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
		if (maxSize > 500) return 16.0f;
		if (maxSize > 300) return 12.0f;
		if (maxSize > 200) return 10.0f;
		if (maxSize > 100) return 8.0f;
		if (maxSize > 50) return 6.0f;
		return 4.0f;
	}

	public static float GetRoadWidthForTerrain(int length, int width)
	{
		float roadWidth = Mathf.Max(length, width) * DefaultRoadWidthPercent;
		return Mathf.Clamp(roadWidth, MinRoadWidthWorld, MaxRoadWidthWorld);
	}
}
