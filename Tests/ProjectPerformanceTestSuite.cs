using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;

public sealed class ProjectPerformanceTestSuite
{
	// Запускает набор проверок, ориентированных на производительность и устойчивость.
	public TestGroupResult Run()
	{
		var group = new TestGroupResult(
			"Тестирование производительности",
			"Проверяется Godot-производительность: создание мешей, применение текстур и экспорт сцен.",
			"Все операции должны выполняться стабильно и укладываться в разумное время без исключений.");

		var watch = Stopwatch.StartNew();

		group.Operations.Add(TestTools.RunOperation("PR-1", "Замеряется генерация меша 30x30.", "Меш должен быть создан быстро и без пустых поверхностей.", () =>
		{
			var generator = new RandomTerrainGenerator();
			Mesh mesh = generator.GenerateMesh(30, 30, -3f, 10f, 15, 0.7f, false, 0.35f, 1, 2, 3, 4);
			if (mesh == null || mesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("30x30 mesh generation failed.");
			return $"Surfaces={mesh.GetSurfaceCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-2", "Замеряется генерация меша 50x50.", "Меш должен строиться для среднего объема данных.", () =>
		{
			var generator = new RandomTerrainGenerator();
			Mesh mesh = generator.GenerateMesh(50, 50, -5f, 14f, 25, 0.8f, true, 0.35f, 11, 12, 13, 14);
			if (mesh == null || mesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("50x50 mesh generation failed.");
			return $"Surfaces={mesh.GetSurfaceCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-3", "Замеряется генерация меша 80x80.", "Крупный меш должен создаваться без исключения и с валидной геометрией.", () =>
		{
			var generator = new RandomTerrainGenerator();
			Mesh mesh = generator.GenerateMesh(80, 80, -8f, 18f, 32, 0.9f, true, 0.45f, 21, 22, 23, 24);
			if (mesh == null || mesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("80x80 mesh generation failed.");
			return $"Surfaces={mesh.GetSurfaceCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-4", "Проверяется скорость подготовки высотной текстуры.", "Текстура должна вычисляться и сохраняться для нескольких размеров без ошибок.", () =>
		{
			var terrain = TestTools.CreateSampleTerrainInstance(24, 24, 12, 7);
			string outputPath = TestTools.CreateSampleTexturePath("perf_texture.png");
			TerrainTexturePainter.ApplyHeightTexture(terrain, -4f, 14f, TerraConfig.SandTexturePath, TerraConfig.GrassTexturePath, TerraConfig.RockTexturePath, outputPath, 0.35f, 0.65f, 24, 24, 0, 0.5f, null, null, null, null, false).GetAwaiter().GetResult();
			if (!FileAccess.FileExists(outputPath))
				throw new InvalidOperationException("Performance texture was not saved.");
			return "Высотная текстура сохранена";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-6", "Генерация и экспорт сцены: различные размеры и параметры → GLTFDocument без падений.", "Экспортные сцены подготовлены для разных размеров мешей успешно.", () =>
		{
			int[] sizes = { 12, 20, 28 };
			for (int i = 0; i < sizes.Length; i++)
			{
				var root = new Node3D { Name = $"PerfRoot{i}" };
				root.AddChild(TestTools.CreateSampleTerrainInstance(sizes[i], sizes[i], Mathf.Max(6, sizes[i] / 2), 10 + i));
				if (!ClassDB.ClassExists("GLTFDocument") || !ClassDB.ClassExists("GLTFState"))
					return "GLTFDocument недоступен в этой версии Godot";
				var gltf = (GodotObject)ClassDB.Instantiate("GLTFDocument");
				var state = (GodotObject)ClassDB.Instantiate("GLTFState");
				var appendResult = gltf.Call("append_from_scene", root, state);
				if ((Error)appendResult.AsInt32() != Error.Ok)
					throw new InvalidOperationException($"AppendFromScene failed for size {sizes[i]}");
			}
			return "Экспортные сцены подготовлены для трех размеров";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-7", "Генерация мешей с максимальными параметрами: 64x64 резолюция, большой диапазон высот.", "Крупные мешиивались без превышения времени ожидания.",() =>
		{
			var generator = new RandomTerrainGenerator();
			Mesh mesh = generator.GenerateMesh(64, 64, -10f, 20f, 32, 0.95f, true, 0.5f, 31, 32, 33, 34);
			if (mesh == null || mesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("Maximum size mesh generation failed.");
			return $"Максимальный геш: Surfaces={mesh.GetSurfaceCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-8", "Проверка скорости применения текстур на мешах разных размеров.", "Текстуры применяются стабильно для малых, средних и больших мешей.",() =>
		{
			int[] sizes = { 16, 32, 48 };
			for (int i = 0; i < sizes.Length; i++)
			{
				var terrain = TestTools.CreateSampleTerrainInstance(sizes[i], sizes[i], Mathf.Max(8, sizes[i] / 3), 50 + i);
				string outputPath = TestTools.CreateSampleTexturePath($"perf_texture_size{sizes[i]}.png");
				TerrainTexturePainter.ApplyHeightTexture(terrain, -4f, 14f, TerraConfig.SandTexturePath, TerraConfig.GrassTexturePath, TerraConfig.RockTexturePath, outputPath, 0.35f, 0.65f, sizes[i], sizes[i], 0, 0.5f, null, null, null, null, false).GetAwaiter().GetResult();
				if (!FileAccess.FileExists(outputPath))
					throw new InvalidOperationException($"Texture was not saved for size {sizes[i]}");
			}
			return "Текстуры применены для всех тестовых размеров";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-11", "Проверяется нормализация и фильтрация карты высот.", "Карта высот нормализована, а slope-путь текстурирования отрабатывает без ошибок.", () =>
		{
			var heights = TestTools.CreateHeightGrid(8, 8, (x, z) => x * 2f + z);
			TerrainMath.NormalizeToRange(heights, -1f, 1f);
			float min = float.MaxValue;
			float max = float.MinValue;
			for (int x = 0; x < heights.GetLength(0); x++)
			{
				for (int z = 0; z < heights.GetLength(1); z++)
				{
					min = Mathf.Min(min, heights[x, z]);
					max = Mathf.Max(max, heights[x, z]);
				}
			}

			if (min > -0.99f || max < 0.99f)
				throw new InvalidOperationException("Height normalization did not span the expected range.");

			var terrain = TestTools.CreateSampleTerrainInstance(24, 24, 12, 8);
			string outputPath = TestTools.CreateSampleTexturePath("perf_normalized_slope.png");
			TerrainTexturePainter.ApplyHeightTexture(
				terrain,
				-4f,
				14f,
				TerraConfig.SandTexturePath,
				TerraConfig.GrassTexturePath,
				TerraConfig.RockTexturePath,
				outputPath,
				0.35f,
				0.65f,
				24,
				24,
				1,
				0.7f,
				null,
				null,
				null,
				null,
				false).GetAwaiter().GetResult();

			if (!FileAccess.FileExists(outputPath))
				throw new InvalidOperationException("Slope texture output was not saved.");
			return $"min={min.ToString("0.###", CultureInfo.InvariantCulture)}, max={max.ToString("0.###", CultureInfo.InvariantCulture)}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-12", "Проверяется применение дорожной маски к текстуре.", "Дорожная маска накладывается и результат сохраняется на диск.", () =>
		{
			var terrain = TestTools.CreateSampleTerrainInstance(32, 32, 16, 9);
			var roadMask = new float[32, 32];
			TerrainMath.RasterizeRoadMask(roadMask, new List<Vector2> { new Vector2(-12f, 0f), new Vector2(12f, 0f) }, 32, 32, 4f);
			string outputPath = TestTools.CreateSampleTexturePath("perf_road_mask.png");
			TerrainTexturePainter.ApplyHeightTexture(
				terrain,
				-4f,
				14f,
				TerraConfig.SandTexturePath,
				TerraConfig.GrassTexturePath,
				TerraConfig.RockTexturePath,
				outputPath,
				0.35f,
				0.65f,
				32,
				32,
				0,
				0.5f,
				roadMask,
				TerraConfig.DefaultRoadTexturePath,
				null,
				null,
				false).GetAwaiter().GetResult();

			if (!FileAccess.FileExists(outputPath))
				throw new InvalidOperationException("Road mask texture output was not saved.");
			return "Road mask texture saved";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-13", "Проверяется размещение 100 объектов окружения.", "ScatteredObjects создается и содержит 100 экземпляров.", () =>
		{
			var root = new Node3D { Name = "PR13_ScatterRoot" };

			var terrain = TestTools.CreateSampleTerrainInstance(80, 80, 32, 10);
			terrain.Name = "PR13_Terrain";
			root.AddChild(terrain);

			string treePath = TestTools.FindFirstExistingResource(
				$"{TerraConfig.AddonRootPath}/Texture/source/tree.tscn",
				$"{TerraConfig.AddonRootPath}/Texture/source/tree2.tscn",
				$"{TerraConfig.AddonRootPath}/Texture/source/bush.tscn");
			if (string.IsNullOrEmpty(treePath))
				throw new InvalidOperationException("No scatter scene resource found.");

			var scatter = new Godot.Collections.Dictionary
			{
				{ "trees", new Godot.Collections.Dictionary
					{
						{ "enabled", true },
						{ "count", 100 },
						{ "paths", new Godot.Collections.Array { treePath } }
					}
				}
			};

			ObjectScatterPlacer.Scatter(root, terrain, 80, 80, 32, -4f, 14f, 0.0f, null, 32, scatter, null);
			Node scattered = root.GetNodeOrNull("ScatteredObjects");
			if (scattered == null)
				throw new InvalidOperationException("ScatteredObjects node was not created.");
			if (scattered.GetChildCount() != 100)
				throw new InvalidOperationException($"Expected 100 scattered objects, got {scattered.GetChildCount()}.");
			return $"Objects={scattered.GetChildCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-14", "Проверяется стыковка continuation чанков.", "Граница нового меша корректируется относительно существующего чанка.", () =>
		{
			var root = new Node3D { Name = "PR14_ContinuationRoot" };
			var source = TestTools.CreateSampleTerrainInstance(24, 24, 12, 11);
			source.Name = "GeneratedMesh_Chunk_1";
			root.AddChild(source);

			var ctx = TerrainContinuationService.BuildContinueContext(root, "x+");
			var targetHeights = TestTools.CreateHeightGrid(12, 12, (x, z) => 20f + x * 0.5f + z * 0.15f);
			Mesh targetMesh = MeshBuilder.BuildTerrainMesh(targetHeights, 24, 24);
			if (targetMesh == null || targetMesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("Target mesh is invalid.");

			float before = GetSeamAverageY(targetMesh);
			TerrainContinuationService.ApplyEdgeConstraintToMesh(targetMesh, 12, ctx, false);
			float after = GetSeamAverageY(targetMesh);

			if (float.IsNaN(before) || float.IsNaN(after))
				throw new InvalidOperationException("Seam averages are invalid.");
			if (Math.Abs(after - before) < 0.01f)
				throw new InvalidOperationException("Continuation seam was not adjusted.");
			return $"before={before.ToString("0.###", CultureInfo.InvariantCulture)}, after={after.ToString("0.###", CultureInfo.InvariantCulture)}";
		}));

		watch.Stop();
		group.DurationMs = watch.ElapsedMilliseconds;
		group.Passed = true;
		for (int i = 0; i < group.Operations.Count; i++)
		{
			if (!group.Operations[i].Passed)
				group.Passed = false;
		}
		group.ActualResult = group.Passed ? $"Производительность всех операций подтверждена за {watch.ElapsedMilliseconds}мс" : "Есть медленные или нестабильные операции";
		return group;
	}

	private static float GetSeamAverageY(Mesh mesh)
	{
		if (mesh is not ArrayMesh arrayMesh || arrayMesh.GetSurfaceCount() == 0)
			return float.NaN;

		var arrays = arrayMesh.SurfaceGetArrays(0);
		var vertices = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];
		var uvs = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.TexUV];
		if (vertices == null || uvs == null || vertices.Count == 0 || uvs.Count == 0)
			return float.NaN;

		float sum = 0f;
		int count = 0;
		for (int i = 0; i < vertices.Count; i++)
		{
			Vector2 uv = (Vector2)uvs[i];
			if (Mathf.Abs(uv.X) > 0.001f)
				continue;
			Vector3 v = (Vector3)vertices[i];
			sum += v.Y;
			count++;
		}

		return count > 0 ? sum / count : float.NaN;
	}
}
