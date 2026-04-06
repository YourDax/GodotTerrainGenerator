using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

// Класс для генерации реального рельефа по данным API OpenTopoData
public static class RealMapTerrainGenerator
{
	// URL API для запроса высот

	// Параметры пакетной отправки запросов
	private const int FIXED_RESOLUTION = 50; // Фиксированное разрешение 50x50
	private const int MAX_POINTS_PER_REQUEST = TerraConfig.OpenTopoMaxPointsPerRequest; // Лимит API OpenTopoData
	private const int MAX_REQUESTS = 25; // Максимум запросов (50x50 = 2500 точек / 100 = 25 запросов)
	private const int REQUEST_DELAY_MS = TerraConfig.OpenTopoRequestDelayMs; // Задержка после успешного запроса
	private const int RETRY_DELAY_MS = TerraConfig.OpenTopoRetryDelayMs; // Задержка при повторной попытке
	private const int MAX_RETRIES = TerraConfig.OpenTopoMaxRetries; // Количество повторных попыток при ошибке
	private const int REQUEST_TIMEOUT_SECONDS = TerraConfig.OpenTopoTimeoutSeconds; // Таймаут запроса

	// Диапазон нормализованных высот (в метрах)
	private const float TARGET_MIN_HEIGHT = 0f;
	private const float TARGET_MAX_HEIGHT = 200f;

	// Параметры масштабирования меша
	private const float MAX_MESH_UNITS = 200f;
	private const float MIN_MESH_UNITS = 8f;
	private const float METERS_TO_UNITS = 0.01f;
	private const float VERTICAL_SCALE = 1.0f; // Будет переопределен динамически
	private const float HEIGHT_TO_MESH_RATIO = 0.15f; // Высоты занимают 15% от размера меша

	// Режимы разрешения
	public enum ResolutionMode
	{
		HighQuality = 0,    // 50x50 (25 запросов)
		MediumQuality = 1,  // 31x31 (10 запросов)
		Adaptive = 2        // Адаптивное
	}

	// Делегат для обновления прогресса
	public delegate void ProgressCallback(float progress, string status);

	// Основной метод генерации рельефа
	public static async Task<Node3D> Generate(
		Node3D parent,
		float leftUpLat,
		float leftUpLng,
		float rightDownLat,
		float rightDownLng,
		Node owner,
		int resolutionMode = 0,
		float realMapWaterLevel = 0.15f,
		bool useSandTexture = true,
		bool useGrassTexture = true,
		bool useRockTexture = true,
		string sandTexturePath = "",
		string grassTexturePath = "",
		string rockTexturePath = "",
		float objectSpacingMultiplier = 0.70f,
		ProgressCallback progressCallback = null
	)
	{
		// Логирование границ
		GD.Print("=== Генерация реального рельефа OpenTopoData ===");
		GD.Print($"Input bounds raw: NW({leftUpLat.ToString(CultureInfo.InvariantCulture)},{leftUpLng.ToString(CultureInfo.InvariantCulture)}) SE({rightDownLat.ToString(CultureInfo.InvariantCulture)},{rightDownLng.ToString(CultureInfo.InvariantCulture)})");

		progressCallback?.Invoke(5.0f, "Загрузка высотных данных...");

		// Нормализуем координаты в N/S/W/E
		float north = Mathf.Max(leftUpLat, rightDownLat);
		float south = Mathf.Min(leftUpLat, rightDownLat);
		float west = Mathf.Min(leftUpLng, rightDownLng);
		float east = Mathf.Max(leftUpLng, rightDownLng);

		// Загружаем матрицу высот (только OpenTopoData)
		int resolution = TerrainMath.ResolveResolution(north, south, west, east, resolutionMode);
		float[,] heights = await RequestHeightsFromOpenTopo(north, south, west, east, resolution, progressCallback);

		// Проверка на ошибки
		if (heights == null)
		{
			GD.PrintErr("❌ heights = null");
			return null;
		}

		// Вывод статистики
		PrintStats("После загрузки", heights);

		progressCallback?.Invoke(70.0f, "Обработка данных...");

		// Заполняем отсутствующие значения
		FillMissingHeights(heights);

		progressCallback?.Invoke(75.0f, "Построение меша...");
		
		// Строим меш на основе высот и получаем размер меша
		float meshMaxSizeUnits;
		RealMapMeshMeta meta;
		Mesh mesh = BuildCenteredMesh(heights, north, south, west, east, out meshMaxSizeUnits, out meta);

		// Проверка валидности меша
		if (mesh == null || mesh.GetSurfaceCount() == 0)
		{
			GD.PrintErr("❌ mesh пуст");
			return null;
		}

		progressCallback?.Invoke(80.0f, "Создание экземпляра меша...");

		// Создаём MeshInstance3D
		var meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			Name = "GeneratedTerrain"
		};

		// Без поворота: сохраняем естественную ориентацию осей карты
		// X: запад -> восток, Z: юг -> север (см. BuildCenteredMesh).

		// Добавляем в сцену
		parent.AddChild(meshInstance);
		if (owner != null) meshInstance.Owner = owner;

		GD.Print("✅ MeshInstance добавлен в сцену");

		// Получаем размеры сетки из размеров массива высот
		int meshResX = heights.GetLength(0);
		int meshResZ = heights.GetLength(1);
		
		GD.Print($"🎨 Applying textures: mesh resolution {meshResX}x{meshResZ}");

		progressCallback?.Invoke(85.0f, "Применение текстур...");

		// Накладываем текстуры по высоте используя специальный класс для реального мира
		// Передаем исходный массив высот напрямую, чтобы избежать проблем с порядком вершин
		const float SAND_GRASS_THRESHOLD = 0.35f;
		const float GRASS_ROCK_THRESHOLD = 0.65f;
		ResolveTexturePaths(
			useSandTexture,
			useGrassTexture,
			useRockTexture,
			sandTexturePath,
			grassTexturePath,
			rockTexturePath,
			out string sandPath,
			out string grassPath,
			out string rockPath
		);
		RealWorldTexturePainter.ApplyHeightTexture(
			meshInstance,
			heights,
			meshResX,
			meshResZ,
			sandPath,
			grassPath,
			rockPath,
			SAND_GRASS_THRESHOLD,
			GRASS_ROCK_THRESHOLD
		);

		GD.Print("✅ Текстуры применены");

		progressCallback?.Invoke(92.0f, "OSM: загрузка объектов...");

		// Вода: используем большой плэйн, но добавляем его только если OSM нашёл воду.
		float waterT = Mathf.Clamp(realMapWaterLevel, 0f, 1f);
		float worldWaterY = Mathf.Lerp(meta.MinVy, meta.MaxVy, waterT);

		// OSM: получаем воду и деревья
		List<OsmOverpassClient.OsmNode> trees;
		List<List<Vector2>> waterPolys;
		using (var http = new System.Net.Http.HttpClient())
		{
			http.Timeout = TimeSpan.FromSeconds(10);
			http.DefaultRequestHeaders.Add("User-Agent", "GodotTerrainPlugin/1.0");
			var osm = new OsmOverpassClient(http);
			waterPolys = await osm.FetchWaterPolygonsAsync(south, west, north, east, 10, (p, s) => progressCallback?.Invoke(92f + p * 0.03f, s));
			trees = await osm.FetchTreeNodesAsync(south, west, north, east, 10, (p, s) => progressCallback?.Invoke(95f + p * 0.03f, s));
		}

		if (waterPolys != null && waterPolys.Count > 0)
		{
			GD.Print($"🌊 OSM: вода найдена (полигонов: {waterPolys.Count}), добавляю плоскость воды");
			var water = new RandomTerrainGenerator().GenerateWaterPlane((int)Mathf.Round(meta.WidthUnits), (int)Mathf.Round(meta.DepthUnits), worldWaterY);
			water.Name = "WaterPlane";
			parent.AddChild(water);
			if (owner != null) water.Owner = owner;
		}
		else
		{
			GD.Print("🌊 OSM: вода не найдена, плоскость воды не добавляется");
		}

		progressCallback?.Invoke(98.0f, "OSM: размещение деревьев...");
		PlaceTreesFromOsm(meshInstance, meta, heights, trees, owner, worldWaterY, objectSpacingMultiplier);

		progressCallback?.Invoke(100.0f, "Генерация завершена!");
		GD.Print("✅ Real-map generation completed");


		return meshInstance;
	}

	private static int ResolveResolution(float north, float south, float west, float east, int resolutionMode)
	{
		return TerrainMath.ResolveResolution(north, south, west, east, resolutionMode);
	}

	private static void ResolveTexturePaths(
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

	private static async Task<float[,]> RequestHeightsFromOpenTopo(
		float north,
		float south,
		float west,
		float east,
		int resolution,
		ProgressCallback progressCallback
	)
	{
		using var http = new System.Net.Http.HttpClient();
		http.Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS);
		http.DefaultRequestHeaders.Add("User-Agent", "GodotTerrainPlugin/1.0");
		var topo = new OpenTopoDataClient(http);
		return await topo.FetchHeightsGridAsync(
			north, west, south, east,
			resolution,
			MAX_POINTS_PER_REQUEST,
			MAX_REQUESTS,
			REQUEST_DELAY_MS,
			MAX_RETRIES,
			RETRY_DELAY_MS,
			TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS),
			(p, s) => progressCallback?.Invoke(5f + p * 0.65f, s)
		);
	}

	private readonly record struct RealMapMeshMeta(
		float North,
		float South,
		float West,
		float East,
		int ResX,
		int ResZ,
		float WidthUnits,
		float DepthUnits,
		float MaxSizeUnits,
		float HeightScale,
		float MinH,
		float MaxH,
		float MinVy,
		float MaxVy
	);

	private readonly record struct PlacedCircle(Vector2 Pos, float Radius);

	private static void PlaceTreesFromOsm(
		MeshInstance3D terrainMesh,
		RealMapMeshMeta meta,
		float[,] heightsMeters,
		List<OsmOverpassClient.OsmNode> trees,
		Node owner,
		float worldWaterY,
		float objectSpacingMultiplier
	)
	{
		if (terrainMesh == null || trees == null || trees.Count == 0)
			return;

		var group = new Node3D { Name = "OSMObjects" };
		terrainMesh.GetParent().AddChild(group);
		if (owner != null) group.Owner = owner;

		var treesRoot = new Node3D { Name = "Trees" };
		group.AddChild(treesRoot);
		if (owner != null) treesRoot.Owner = owner;

		// Дефолтные сцены деревьев
		var treeScenes = new List<PackedScene>();
		const string tree1Path = "res://addons/terragenerating/Texture/source/tree.tscn";
		const string tree2Path = "res://addons/terragenerating/Texture/source/tree2.tscn";
		if (ResourceLoader.Exists(tree1Path)) treeScenes.Add(ResourceLoader.Load<PackedScene>(tree1Path));
		if (ResourceLoader.Exists(tree2Path)) treeScenes.Add(ResourceLoader.Load<PackedScene>(tree2Path));

		// Масштабируем объекты относительно размера карты.
		float baseScaleFactor = Mathf.Clamp(meta.MaxSizeUnits / 1000f, 0.005f, 0.20f);
		// Пересечения проверяем после вычисления финального scale каждой модели.
		float spacingMul = Mathf.Clamp(objectSpacingMultiplier, 0.20f, 3.00f);
		float cellSize = Mathf.Max(0.5f, meta.MaxSizeUnits / 120f);
		float overlapPadding = Mathf.Max(0.03f, meta.MaxSizeUnits * 0.0005f);
		float maxPlacedRadius = 0.5f;
		var spatial = new Dictionary<Vector2I, List<PlacedCircle>>();

		for (int i = 0; i < trees.Count; i++)
		{
			var n = trees[i];
			Vector2 uv = TerrainMath.LonLatToUv(n.Lat, n.Lon, meta.North, meta.South, meta.West, meta.East);
			float u = uv.X;
			float v = uv.Y;
			if (float.IsNaN(u) || float.IsNaN(v)) continue;
			if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

			// Семплим высоту в метрах билинейно по сетке
			float hx = u * (meta.ResX - 1);
			float hz = v * (meta.ResZ - 1);
			int x0 = Mathf.Clamp((int)Mathf.Floor(hx), 0, meta.ResX - 1);
			int z0 = Mathf.Clamp((int)Mathf.Floor(hz), 0, meta.ResZ - 1);
			int x1 = Mathf.Min(x0 + 1, meta.ResX - 1);
			int z1 = Mathf.Min(z0 + 1, meta.ResZ - 1);
			float tx = hx - x0;
			float tz = hz - z0;

			float h00 = heightsMeters[x0, z0];
			float h10 = heightsMeters[x1, z0];
			float h01 = heightsMeters[x0, z1];
			float h11 = heightsMeters[x1, z1];
			float hm = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
			if (float.IsNaN(hm)) continue;

			float vy = (hm - meta.MinH) * meta.HeightScale;

			// Преобразуем u/v в локальные x/z меша (как в BuildCenteredMesh)
			Vector3 local = TerrainMath.UvToLocal(u, v, meta.WidthUnits, meta.DepthUnits, vy);
			Vector3 world = terrainMesh.GlobalTransform * local;

			if (world.Y <= worldWaterY + 0.05f)
				continue;

			Node3D tree;
			if (treeScenes.Count > 0)
			{
				int pick = (int)Mathf.Floor(GD.Randf() * treeScenes.Count);
				pick = Mathf.Clamp(pick, 0, treeScenes.Count - 1);
				tree = treeScenes[pick].Instantiate<Node3D>();
			}
			else
			{
				var mi = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.15f, Height = 2.2f } };
				mi.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.7f, 0.2f) };
				tree = new Node3D();
				tree.AddChild(mi);
			}

			tree.Name = $"tree_{i}";
			treesRoot.AddChild(tree);
			if (owner != null) tree.Owner = owner;

			float yaw = GD.Randf() * Mathf.Tau;
			// Небольшой рандом только для естественности, без сильного разброса размера.
			float jitter = Mathf.Lerp(0.96f, 1.04f, GD.Randf());
			float scaleFactor = baseScaleFactor * jitter;

			float localRadius = EstimateFootprintRadius(tree);
			float candidateRadius = Mathf.Max(0.08f, localRadius * scaleFactor);
			var p2 = new Vector2(world.X, world.Z);
			var cell = new Vector2I(
				Mathf.FloorToInt(p2.X / cellSize),
				Mathf.FloorToInt(p2.Y / cellSize)
			);
			int searchRange = Mathf.Max(1, Mathf.CeilToInt(((candidateRadius + maxPlacedRadius + overlapPadding) * spacingMul) / cellSize));
			bool tooClose = false;
			for (int dz = -searchRange; dz <= searchRange && !tooClose; dz++)
			{
				for (int dx = -searchRange; dx <= searchRange && !tooClose; dx++)
				{
					var nc = new Vector2I(cell.X + dx, cell.Y + dz);
					if (!spatial.TryGetValue(nc, out var bucket)) continue;
					for (int bi = 0; bi < bucket.Count; bi++)
					{
						PlacedCircle other = bucket[bi];
						float minDist = (candidateRadius + other.Radius + overlapPadding) * spacingMul;
						if (other.Pos.DistanceTo(p2) < minDist)
						{
							tooClose = true;
							break;
						}
					}
				}
			}
			if (tooClose)
			{
				tree.QueueFree();
				continue;
			}

			Basis worldBasis = Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(scaleFactor, scaleFactor, scaleFactor));
			tree.GlobalTransform = new Transform3D(worldBasis, world);

			if (!spatial.TryGetValue(cell, out var list))
			{
				list = new List<PlacedCircle>();
				spatial[cell] = list;
			}
			list.Add(new PlacedCircle(p2, candidateRadius));
			if (candidateRadius > maxPlacedRadius) maxPlacedRadius = candidateRadius;
		}
	}

	private static float EstimateFootprintRadius(Node3D root)
	{
		bool has = false;
		Aabb merged = default;
		CollectMeshAabbs(root, Transform3D.Identity, ref has, ref merged);
		if (!has)
			return 0.4f;

		float dx = merged.Size.X * 0.5f;
		float dz = merged.Size.Z * 0.5f;
		return Mathf.Max(0.1f, Mathf.Sqrt(dx * dx + dz * dz));
	}

	private static void CollectMeshAabbs(Node3D node, Transform3D rootToNode, ref bool has, ref Aabb merged)
	{
		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			Aabb aabb = TransformAabb(rootToNode, mi.GetAabb());
			if (!has)
			{
				merged = aabb;
				has = true;
			}
			else
			{
				merged = merged.Merge(aabb);
			}
		}

		foreach (Node child in node.GetChildren())
		{
			if (child is Node3D child3d)
			{
				CollectMeshAabbs(child3d, rootToNode * child3d.Transform, ref has, ref merged);
			}
		}
	}

	private static Aabb TransformAabb(Transform3D transform, Aabb aabb)
	{
		Vector3 p0 = transform * aabb.Position;
		Vector3 p1 = transform * (aabb.Position + new Vector3(aabb.Size.X, 0, 0));
		Vector3 p2 = transform * (aabb.Position + new Vector3(0, aabb.Size.Y, 0));
		Vector3 p3 = transform * (aabb.Position + new Vector3(0, 0, aabb.Size.Z));
		Vector3 p4 = transform * (aabb.Position + new Vector3(aabb.Size.X, aabb.Size.Y, 0));
		Vector3 p5 = transform * (aabb.Position + new Vector3(aabb.Size.X, 0, aabb.Size.Z));
		Vector3 p6 = transform * (aabb.Position + new Vector3(0, aabb.Size.Y, aabb.Size.Z));
		Vector3 p7 = transform * (aabb.Position + aabb.Size);

		Vector3 min = p0;
		Vector3 max = p0;
		void Include(Vector3 p)
		{
			min = min.Min(p);
			max = max.Max(p);
		}

		Include(p1); Include(p2); Include(p3); Include(p4);
		Include(p5); Include(p6); Include(p7);
		return new Aabb(min, max - min);
	}

	// Вода теперь добавляется большим плэйном, но только если OSM нашёл воду.

	// Заполнение пропущенных значений высот
	private static void FillMissingHeights(float[,] data)
	{
		int resX = data.GetLength(0);
		int resZ = data.GetLength(1);

		// Один проход интерполяции соседями
		for (int z = 0; z < resZ; z++)
		{
			for (int x = 0; x < resX; x++)
			{
				if (!float.IsNaN(data[x, z])) continue;

				float sum = 0;
				int count = 0;

				// Локальный метод добавления соседей
				void TryAdd(int xx, int zz)
				{
					if (xx >= 0 && xx < resX && zz >= 0 && zz < resZ && !float.IsNaN(data[xx, zz]))
					{
						sum += data[xx, zz];
						count++;
					}
				}

				// Смотрим вокруг клетки
				TryAdd(x - 1, z);
				TryAdd(x + 1, z);
				TryAdd(x, z - 1);
				TryAdd(x, z + 1);
				TryAdd(x - 1, z - 1);
				TryAdd(x + 1, z - 1);
				TryAdd(x - 1, z + 1);
				TryAdd(x + 1, z + 1);

				// Если есть валидные соседи — усредняем
				if (count > 0)
				{
					data[x, z] = sum / count;
				}
			}
		}

		// Оставшиеся NaN заменяем на 0
		for (int z = 0; z < resZ; z++)
			for (int x = 0; x < resX; x++)
				if (float.IsNaN(data[x, z])) data[x, z] = 0f;
	}

	// Построение меша на основе матрицы высот
	private static Mesh BuildCenteredMesh(float[,] heights, float north, float south, float west, float east, out float sizeUnits, out RealMapMeshMeta meta)
	{
		int resX = heights.GetLength(0);
		int resZ = heights.GetLength(1);

		// Средняя широта в радианах
		float meanLat = (north + south) * 0.5f;
		meanLat = Mathf.DegToRad(meanLat);
		const float METERS_PER_DEGREE_LAT = 111320f;
		float metersPerDegLon = Mathf.Cos(meanLat) * METERS_PER_DEGREE_LAT;

		// Вычисляем реальные размеры в метрах
		float widthMeters = Math.Abs(east - west) * metersPerDegLon;
		float depthMeters = Math.Abs(north - south) * METERS_PER_DEGREE_LAT;

		// Вычисляем размеры меша в юнитах по каждой оси (сохраняем форму bbox)
		float widthUnits = Mathf.Max(widthMeters * METERS_TO_UNITS, MIN_MESH_UNITS);
		float depthUnits = Mathf.Max(depthMeters * METERS_TO_UNITS, MIN_MESH_UNITS);
		if (float.IsNaN(widthUnits) || widthUnits <= 0f) widthUnits = MIN_MESH_UNITS;
		if (float.IsNaN(depthUnits) || depthUnits <= 0f) depthUnits = MIN_MESH_UNITS;

		// Масштабирование так, чтобы максимальная сторона не превышала MAX_MESH_UNITS
		float maxSideUnits = Mathf.Max(widthUnits, depthUnits);
		float finalScale = maxSideUnits > MAX_MESH_UNITS ? (MAX_MESH_UNITS / maxSideUnits) : 1f;
		widthUnits *= finalScale;
		depthUnits *= finalScale;
		sizeUnits = Mathf.Max(widthUnits, depthUnits);

		// Шаг между вершинами
		float stepX = widthUnits / (resX - 1);
		float stepZ = depthUnits / (resZ - 1);

		// Логируем масштабирование
		GD.Print($"✅ Mesh scaled: final width={widthUnits:F2} depth={depthUnits:F2} units");
		GD.Print($"Real size (m): width={widthMeters:F1}, depth={depthMeters:F1} -> mesh units width={widthUnits:F2}, depth={depthUnits:F2}");

		// Создаём SurfaceTool
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		// Массивы вершин и UV
		Vector3[] verts = new Vector3[resX * resZ];
		Vector2[] uvs = new Vector2[resX * resZ];

		float halfX = widthUnits * 0.5f;
		float halfZ = depthUnits * 0.5f;

		// Получаем min/max высот (метры)
		GetMinMax(heights, out float minH, out float maxH);

		// Вычисляем масштаб для высот: масштабируем пропорционально размеру меша
		// Это гарантирует, что рельеф будет заметен независимо от размера меша
		float heightRange = maxH - minH;
		
		// Вычисляем масштаб так, чтобы максимальная высота занимала HEIGHT_TO_MESH_RATIO от размера меша
		float targetMaxHeight = sizeUnits * HEIGHT_TO_MESH_RATIO;
		float heightScale = heightRange > 0.001f ? targetMaxHeight / heightRange : 1f;
		
		GD.Print($"📏 Heights: min={minH:F1}m, max={maxH:F1}m, range={heightRange:F1}m");
		GD.Print($"📏 Mesh size: {sizeUnits:F2} units, target max height: {targetMaxHeight:F2} units");
		GD.Print($"📏 Height scale: {heightScale:F6} (height range {heightRange:F1}m -> {targetMaxHeight:F2} units)");
		
		// Проверяем, есть ли вариация в высотах
		if (heightRange < 1.0f)
		{
			GD.PrintErr($"⚠️ ВНИМАНИЕ: Очень маленький диапазон высот ({heightRange:F2}m)! Ландшафт будет плоским.");
		}

		float minVy = float.MaxValue;
		float maxVy = float.MinValue;

		// Генерируем вершины и UV
		for (int z = 0; z < resZ; z++)
		{
			for (int x = 0; x < resX; x++)
			{
				float vx = x * stepX - halfX;
				// z=0 (north) -> +Z, z=max (south) -> -Z
				float vz = halfZ - z * stepZ;
				
				// Высоты в метрах преобразуем в юниты Godot
				float height = heights[x, z];
				if (float.IsNaN(height))
				{
					height = minH; // Используем минимальную высоту для NaN
				}
				
				// Преобразуем высоту из метров в юниты Godot
				// Вычитаем minH чтобы начать с нуля, затем масштабируем пропорционально размеру меша
				float heightInMeters = height - minH; // Относительная высота от минимума
				float vy = heightInMeters * heightScale;
				if (vy < minVy) minVy = vy;
				if (vy > maxVy) maxVy = vy;
				
				// Логируем для отладки (только первые несколько вершин)
				if (x < 3 && z < 3)
				{
					GD.Print($"Vertex [{x},{z}]: height={height:F1}m, relative={heightInMeters:F1}m, vy={vy:F3} units");
				}

				int idx = z * resX + x;
				verts[idx] = new Vector3(vx, vy, vz);
				uvs[idx] = new Vector2((float)x / (resX - 1), (float)z / (resZ - 1));
			}
		}

		// Создаём треугольники (индексы)
		for (int z = 0; z < resZ - 1; z++)
		{
			for (int x = 0; x < resX - 1; x++)
			{
				int i0 = z * resX + x;
				int i1 = i0 + 1;
				int i2 = i0 + resX;
				int i3 = i2 + 1;

				st.SetUV(uvs[i0]); st.AddVertex(verts[i0]);
				st.SetUV(uvs[i2]); st.AddVertex(verts[i2]);
				st.SetUV(uvs[i1]); st.AddVertex(verts[i1]);

				st.SetUV(uvs[i1]); st.AddVertex(verts[i1]);
				st.SetUV(uvs[i2]); st.AddVertex(verts[i2]);
				st.SetUV(uvs[i3]); st.AddVertex(verts[i3]);
			}
		}

		// Генерируем нормали
		st.GenerateNormals();

		// Возвращаем меш и размер
		meta = new RealMapMeshMeta(north, south, west, east, resX, resZ, widthUnits, depthUnits, sizeUnits, heightScale, minH, maxH, minVy, maxVy);
		return st.Commit();
	}

	// Нормализация значений в диапазон
	private static void NormalizeToRange(float[,] h, float minTarget, float maxTarget)
	{
		GetMinMax(h, out float min, out float max);

		if (Math.Abs(max - min) < 0.001f)
		{
			// Почти нет рельефа — генерируем простой наклон
			for (int x = 0; x < h.GetLength(0); x++)
				for (int z = 0; z < h.GetLength(1); z++)
					h[x, z] = (x + z) * 0.1f;
			return;
		}

		// Линейная нормализация
		for (int x = 0; x < h.GetLength(0); x++)
			for (int z = 0; z < h.GetLength(1); z++)
			{
				float t = Mathf.InverseLerp(min, max, h[x, z]);
				h[x, z] = Mathf.Lerp(minTarget, maxTarget, t);
			}
	}

	// Получение min/max
	private static void GetMinMax(float[,] h, out float min, out float max)
	{
		min = float.MaxValue;
		max = float.MinValue;
		bool any = false;

		for (int z = 0; z < h.GetLength(1); z++)
		{
			for (int x = 0; x < h.GetLength(0); x++)
			{
				float v = h[x, z];
				if (float.IsNaN(v)) continue;
				any = true;
				if (v < min) min = v;
				if (v > max) max = v;
			}
		}

		if (!any)
		{
			min = 0f;
			max = 0f;
		}
	}

	// Логирование статистики
	private static void PrintStats(string label, float[,] h)
	{
		GetMinMax(h, out float min, out float max);
		GD.Print($"[{label}] min={min} max={max} delta={max - min}");
	}


}
