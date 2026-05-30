using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Случайное размещение объектов по террейну: опора по нижнему центру AABB модели, высота по полю высот меша,
/// точка контакта строго выше плоскости воды, без пересечений по XZ, без дорог, лёгкое утопление по нормали.
/// </summary>
public static class ObjectScatterPlacer
{
	private const int MaxAttemptsMultiplier = 400;
	private const float RoadMaskThreshold = 0.02f;
	/// <summary>Утопить опорную точку вдоль наружной нормали к поверхности (мировые единицы).</summary>
	private const float EmbedIntoSurface = 0.08f;

	// Расставляет объекты по поверхности террейна с учётом воды, дорог и дистанции между экземплярами.
	public static void Scatter(
		Node3D terrainRoot,
		MeshInstance3D terrainMesh,
		int length,
		int width,
		int resolution,
		float minHeight,
		float maxHeight,
		float waterLevel,
		float[,] roadMask,
		int textureResolution,
		Godot.Collections.Dictionary scatterByCategory,
		Node owner
	)
	{
		if (terrainRoot == null || terrainMesh == null || scatterByCategory == null || scatterByCategory.Count == 0)
			return;

		float[,] heights = TerrainMeshSampling.ExtractHeightsFromMesh(terrainMesh, length, width, resolution);
		if (heights == null)
		{
			GD.PrintErr("ObjectScatterPlacer: не удалось извлечь высоты из меша.");
			return;
		}

		// Та же высота плоскости воды в мире, что в TerrainGenerator.GenerateRandomTerrainAsync
		float yOffset = (maxHeight - minHeight) * 0.5f;
		float worldWaterPlaneY = Mathf.Lerp(minHeight, maxHeight, waterLevel) - yOffset;
		// Строго выше плоскости воды (не касаться и не быть ниже/на уровне)
		const float AboveWaterPlaneEpsilon = 0.02f;

		float halfL = length * 0.5f;
		float halfW = width * 0.5f;
		// Отступ от края меша, чтобы не ставить объекты на «обрезанный» край сетки
		float edgeMargin = Mathf.Min(halfL, halfW) * 0.03f;
		edgeMargin = Mathf.Clamp(edgeMargin, 0.2f, Mathf.Min(halfL, halfW) * 0.48f);

		float minSpacing = Mathf.Max(0.6f, Mathf.Min(length, width) * 0.012f);

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		var group = new Node3D { Name = "ScatteredObjects" };
		terrainRoot.AddChild(group);
		if (owner != null)
			group.Owner = owner;

		var placedWorldXz = new List<Vector2>();

		foreach (var kv in scatterByCategory)
		{
			if (kv.Key.VariantType != Variant.Type.String)
				continue;
			string key = kv.Key.AsString();
			if (kv.Value.VariantType != Variant.Type.Dictionary)
				continue;
			var cfg = kv.Value.AsGodotDictionary();
			if (!cfg.TryGetValue("enabled", out var en) || !en.AsBool())
				continue;
			if (!cfg.TryGetValue("count", out var cntVar))
				continue;
			int count = Mathf.Max(0, cntVar.AsInt32());
			if (count == 0)
				continue;
			if (!cfg.TryGetValue("paths", out var pathsVar))
				continue;
			var paths = pathsVar.AsGodotArray();
			var validPaths = new List<string>();
			foreach (var p in paths)
			{
				string s = p.AsString().StripEdges();
				if (!string.IsNullOrEmpty(s))
					validPaths.Add(s);
			}
			if (validPaths.Count == 0)
			{
				GD.PrintErr($"ObjectScatterPlacer: категория «{key}» — нет путей к моделям.");
				continue;
			}

			int placed = 0;
			int maxAttempts = count * MaxAttemptsMultiplier;
			for (int attempt = 0; attempt < maxAttempts && placed < count; attempt++)
			{
				float wx = rng.RandfRange(-halfL + edgeMargin, halfL - edgeMargin);
				float wz = rng.RandfRange(-halfW + edgeMargin, halfW - edgeMargin);

				float h = SampleHeightBilinear(heights, resolution, wx, wz, length, width);

				if (roadMask != null && IsOnRoad(roadMask, textureResolution, length, width, wx, wz))
					continue;

				Vector3 localOnSurface = new Vector3(wx, h, wz);
				// Точка на поверхности террейна в мире (как у узла меша с RotateX и смещением)
				Vector3 worldSurface = terrainMesh.GlobalTransform * localOnSurface;
				if (worldSurface.Y <= worldWaterPlaneY + AboveWaterPlaneEpsilon)
					continue;

				Vector3 localNormal = SampleSurfaceNormalLocal(heights, resolution, wx, wz, length, width);
				Vector3 worldNormal = terrainMesh.GlobalTransform.Basis * localNormal;
				if (worldNormal.LengthSquared() < 1e-8f)
					worldNormal = Vector3.Up;
				else
					worldNormal = worldNormal.Normalized();
				// Наружная нормаль к «небу» (меш перевернут — выравниваем знак)
				if (worldNormal.Dot(Vector3.Up) < 0f)
					worldNormal = -worldNormal;
				Vector3 worldPos = worldSurface - worldNormal * EmbedIntoSurface;
				if (worldPos.Y <= worldWaterPlaneY + AboveWaterPlaneEpsilon)
					continue;

				var candidateXz = new Vector2(worldPos.X, worldPos.Z);
				if (IsTooCloseToAny(placedWorldXz, candidateXz, minSpacing))
					continue;

				string path = validPaths[rng.RandiRange(0, validPaths.Count - 1)];
				Node3D instance = InstantiateModel(path);
				if (instance == null)
					continue;

				float yaw = rng.RandfRange(0f, Mathf.Tau);
				// Только поворот вокруг мировой вертикали — не наследуем Basis террейна (там RotateX(π))
				var uprightBasis = Basis.FromEuler(new Vector3(0f, yaw, 0f));
				// Точка на поверхности, куда должна попасть нижняя центральная точка AABB модели
				Vector3 bottomCenterLocal = GetBottomCenterLocalInRootSpace(instance);
				Vector3 worldOrigin = worldPos - uprightBasis * bottomCenterLocal;

				instance.Name = $"{key}_{placed}";
				group.AddChild(instance);
				if (owner != null)
					instance.Owner = owner;

				instance.GlobalTransform = new Transform3D(uprightBasis, worldOrigin);

				placedWorldXz.Add(candidateXz);
				placed++;
			}

			if (placed < count)
				GD.PrintErr($"ObjectScatterPlacer: «{key}» — размещено только {placed} из {count} (мало подходящих точек или слишком плотно).");
		}
	}

	// Проверяет, не слишком ли близко новая точка к уже размещённым объектам.
	private static bool IsTooCloseToAny(List<Vector2> placed, Vector2 candidate, float minDist)
	{
		float d2 = minDist * minDist;
		for (int i = 0; i < placed.Count; i++)
		{
			if (placed[i].DistanceSquaredTo(candidate) < d2)
				return true;
		}
		return false;
	}

	/// <summary>
	/// Центр нижней грани AABB модели в локальном пространстве корня инстанса (куда должна попасть точка на террейне).
	/// </summary>
	// Находит центр нижней грани модели в локальном пространстве корня инстанса.
	private static Vector3 GetBottomCenterLocalInRootSpace(Node3D root)
	{
		Aabb merged = default;
		bool has = false;
		if (root is MeshInstance3D rmi && rmi.Mesh != null)
		{
			merged = TransformAabb(Transform3D.Identity, rmi.GetAabb());
			has = true;
		}
		CollectMeshesRecursive(root, Transform3D.Identity, ref merged, ref has);
		if (!has)
			return Vector3.Zero;
		return new Vector3(
			merged.Position.X + merged.Size.X * 0.5f,
			merged.Position.Y,
			merged.Position.Z + merged.Size.Z * 0.5f);
	}

	// Собирает AABB всех вложенных MeshInstance3D в корневом пространстве.
	private static void CollectMeshesRecursive(Node3D node, Transform3D rootToNode, ref Aabb merged, ref bool has)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is not Node3D child3d)
				continue;
			Transform3D rootToChild = rootToNode * child3d.Transform;
			if (child is MeshInstance3D mi && mi.Mesh != null)
			{
				Aabb a = TransformAabb(rootToChild, mi.GetAabb());
				if (!has)
				{
					merged = a;
					has = true;
				}
				else
					merged = merged.Merge(a);
			}
			CollectMeshesRecursive(child3d, rootToChild, ref merged, ref has);
		}
	}

	// Переводит AABB в другое пространство с учётом всех восьми углов.
	private static Aabb TransformAabb(Transform3D transform, Aabb aabb)
	{
		Vector3 min = aabb.Position;
		Vector3 max = aabb.Position + aabb.Size;
		Vector3 minOut = transform * new Vector3(min.X, min.Y, min.Z);
		Vector3 maxOut = minOut;
		for (int i = 1; i < 8; i++)
		{
			float x = (i & 1) != 0 ? max.X : min.X;
			float y = (i & 2) != 0 ? max.Y : min.Y;
			float z = (i & 4) != 0 ? max.Z : min.Z;
			Vector3 c = transform * new Vector3(x, y, z);
			minOut = new Vector3(
				Mathf.Min(minOut.X, c.X),
				Mathf.Min(minOut.Y, c.Y),
				Mathf.Min(minOut.Z, c.Z));
			maxOut = new Vector3(
				Mathf.Max(maxOut.X, c.X),
				Mathf.Max(maxOut.Y, c.Y),
				Mathf.Max(maxOut.Z, c.Z));
		}
		return new Aabb(minOut, maxOut - minOut);
	}

	// Оценивает локальную нормаль поверхности по карте высот.
	private static Vector3 SampleSurfaceNormalLocal(
		float[,] heights,
		int resolution,
		float wx,
		float wz,
		int length,
		int width
	)
	{
		float delta = Mathf.Max(length, width) / Mathf.Max(1, resolution - 1);
		delta = Mathf.Max(delta * 0.35f, 0.08f);
		float hx = (SampleHeightBilinear(heights, resolution, wx + delta, wz, length, width)
			- SampleHeightBilinear(heights, resolution, wx - delta, wz, length, width)) / (2f * delta);
		float hz = (SampleHeightBilinear(heights, resolution, wx, wz + delta, length, width)
			- SampleHeightBilinear(heights, resolution, wx, wz - delta, length, width)) / (2f * delta);
		float invLen = 1f / Mathf.Sqrt(hx * hx + 1f + hz * hz);
		return new Vector3(-hx * invLen, invLen, -hz * invLen);
	}

	// Проверяет, попадает ли точка в область дороги по текстурной маске.
	private static bool IsOnRoad(float[,] roadMask, int texRes, int length, int width, float wx, float wz)
	{
		float halfL = length * 0.5f;
		float halfW = width * 0.5f;
		float nx = (wx + halfL) / length;
		float nz = (wz + halfW) / width;
		nx = Mathf.Clamp(nx, 0f, 1f);
		nz = Mathf.Clamp(nz, 0f, 1f);
		int texX = (int)(nx * (texRes - 1));
		int texZ = (int)(nz * (texRes - 1));
		texX = Mathf.Clamp(texX, 0, texRes - 1);
		texZ = Mathf.Clamp(texZ, 0, texRes - 1);
		return roadMask[texX, texZ] > RoadMaskThreshold;
	}

	// Берёт высоту из карты по мировым координатам через билинейную интерполяцию.
	private static float SampleHeightBilinear(float[,] heights, int resolution, float wx, float wz, int length, int width)
	{
		float halfL = length * 0.5f;
		float halfW = width * 0.5f;
		float px = (wx + halfL) / length;
		float pz = (wz + halfW) / width;
		px = Mathf.Clamp(px, 0f, 1f);
		pz = Mathf.Clamp(pz, 0f, 1f);
		float fx = px * (resolution - 1);
		float fz = pz * (resolution - 1);
		int x0 = (int)Mathf.Floor(fx);
		int z0 = (int)Mathf.Floor(fz);
		int x1 = Mathf.Min(x0 + 1, resolution - 1);
		int z1 = Mathf.Min(z0 + 1, resolution - 1);
		float tx = fx - x0;
		float tz = fz - z0;
		float h00 = heights[x0, z0];
		float h10 = heights[x1, z0];
		float h01 = heights[x0, z1];
		float h11 = heights[x1, z1];
		float hx0 = Mathf.Lerp(h00, h10, tx);
		float hx1 = Mathf.Lerp(h01, h11, tx);
		return Mathf.Lerp(hx0, hx1, tz);
	}

	// Загружает ресурс модели и приводит его к Node3D для размещения в сцене.
	private static Node3D InstantiateModel(string path)
	{
		bool exists = path.StartsWith("res://", StringComparison.Ordinal)
			? ResourceLoader.Exists(path)
			: FileAccess.FileExists(path);
		if (!exists)
		{
			GD.PrintErr($"ObjectScatterPlacer: файл не найден: {path}");
			return null;
		}
		var res = ResourceLoader.Load(path);
		if (res == null)
		{
			GD.PrintErr($"ObjectScatterPlacer: не удалось загрузить: {path}");
			return null;
		}
		if (res is PackedScene scene)
		{
			var node = scene.Instantiate();
			return node as Node3D ?? WrapInNode3D(node);
		}
		if (res is Mesh mesh)
		{
			var mi = new MeshInstance3D { Mesh = mesh };
			var holder = new Node3D();
			holder.AddChild(mi);
			return holder;
		}
		GD.PrintErr($"ObjectScatterPlacer: неподдерживаемый тип ресурса для {path}: {res.GetType().Name}");
		return null;
	}

	// Оборачивает произвольный Node в Node3D, чтобы его можно было позиционировать в мире.
	private static Node3D WrapInNode3D(Node node)
	{
		var holder = new Node3D();
		holder.AddChild(node);
		return holder;
	}
}
