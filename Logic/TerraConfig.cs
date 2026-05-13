using Godot;

// Общие пути ресурсов и настройки для аддона.
public static class TerraConfig
{
	private const string LegacyAddonRoot = "res://addons/terragenerating";
	private static string _addonRootCache;

	public static string AddonRootPath => ResolveAddonRootPath();
	public static string SandTexturePath => $"{AddonRootPath}/Texture/sand.png";
	public static string GrassTexturePath => $"{AddonRootPath}/Texture/grass.png";
	public static string RockTexturePath => $"{AddonRootPath}/Texture/rock.png";
	public static string DefaultRoadTexturePath => $"{AddonRootPath}/Texture/road.jpg";

	public const int OpenTopoMaxPointsPerRequest = 100;
	public const int OpenTopoMaxRetries = 5;
	public const int OpenTopoRequestDelayMs = 1000;
	public const int OpenTopoRetryDelayMs = 1000;
	public const int OpenTopoTimeoutSeconds = 4;

	public const float DefaultRoadWidthPercent = 0.02f;
	public const float MinRoadWidthWorld = 1.0f;
	public const float MaxRoadWidthWorld = 5.0f;

	// Возвращает подходящее разрешение текстуры под размер карты.
	public static int GetTextureResolutionForSize(int maxSize)
	{
		if (maxSize > 500) return 4096;
		if (maxSize > 300) return 3072;
		if (maxSize > 200) return 2048;
		if (maxSize > 100) return 1536;
		if (maxSize > 50) return 1280;
		return 1024;
	}

	// Вычисляет масштаб тайлинга текстур для заданного размера карты.
	public static float GetTileScaleForSize(int maxSize)
	{
		// Adaptive continuous formula instead of hard thresholds.
		// Fitted to keep close-up detail on large maps while avoiding over-tiling on small maps.
		// Approx targets: 50 -> ~5, 100 -> ~8, 300 -> ~15, 500 -> ~21, 1200 -> ~36.
		float size = Mathf.Max(1.0f, maxSize);
		float tileScale = 0.439f * Mathf.Pow(size, 0.621f);
		return Mathf.Clamp(tileScale, 5.0f, 60.0f);
	}

	// Подбирает мировую ширину дороги пропорционально размеру террейна.
	public static float GetRoadWidthForTerrain(int length, int width)
	{
		float roadWidth = Mathf.Max(length, width) * DefaultRoadWidthPercent;
		return Mathf.Clamp(roadWidth, MinRoadWidthWorld, MaxRoadWidthWorld);
	}

	// Находит фактический корень аддона, поддерживая старый путь для совместимости.
	private static string ResolveAddonRootPath()
	{
		if (!string.IsNullOrEmpty(_addonRootCache))
			return _addonRootCache;

		// Предпочитаем фактическую папку, где лежит terra_generating_main.gd.
		if (DirAccess.DirExistsAbsolute("res://addons"))
		{
			var dir = DirAccess.Open("res://addons");
			if (dir != null)
			{
				dir.ListDirBegin();
				while (true)
				{
					string name = dir.GetNext();
					if (string.IsNullOrEmpty(name))
						break;
					if (name == "." || name == "..")
						continue;
					if (!dir.CurrentIsDir())
						continue;

					string candidateRoot = $"res://addons/{name}";
					string pluginMain = $"{candidateRoot}/terra_generating_main.gd";
					if (ResourceLoader.Exists(pluginMain))
					{
						_addonRootCache = candidateRoot;
						dir.ListDirEnd();
						return _addonRootCache;
					}
				}
				dir.ListDirEnd();
			}
		}

		_addonRootCache = LegacyAddonRoot;
		return _addonRootCache;
	}
}
