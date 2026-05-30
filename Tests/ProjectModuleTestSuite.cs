using Godot;
using System;
using System.Diagnostics;
using System.Globalization;

public sealed class ProjectModuleTestSuite
{
	// Выполняет набор низкоуровневых модульных проверок ядра генерации.
	public TestGroupResult Run()
	{
		var group = new TestGroupResult(
			"Модульные тесты",
			"Проверяются Godot-зависимые части ядра: MeshBuilder и TerrainContinuationService.",
			"Все Godot-зависимые операции генерации меша и continuation возвращают корректные значения.");

		var watch = Stopwatch.StartNew();
		group.Operations.Add(TestTools.RunOperation(
			"MB-1 MeshBuilder height mesh",
			"Проверяется построение height mesh через MeshBuilder.",
			"MeshBuilder должен возвращать ArrayMesh с поверхностью.",
			() =>
			{
				var baseNoise = new FastNoiseLite { Seed = 11, Frequency = 0.04f };
				var hillNoise = new FastNoiseLite { Seed = 22, Frequency = 0.07f };
				var detailNoise = new FastNoiseLite { Seed = 33, Frequency = 0.14f };
				Mesh mesh = MeshBuilder.BuildHeightMesh(24, 24, -4f, 12f, 12, baseNoise, hillNoise, detailNoise, 0.65f, false, 0.35f, 0.2f, 1f, 0.06f, 0.18f, null, 0f, 0f);
				if (mesh == null || mesh.GetSurfaceCount() == 0)
					throw new InvalidOperationException("MeshBuilder returned an empty mesh.");
				return $"SurfaceCount={mesh.GetSurfaceCount()}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"TC-1 Continue context analysis",
			"Проверяется анализ frontier и контекста continuation.",
			"BuildContinueContext должен вернуть согласованный контекст для x+.",
			() =>
			{
				var root = new Node3D { Name = "ContinuationRoot" };
				var source = TestTools.CreateSampleTerrainInstance(24, 24, 12, 1);
				source.Name = "GeneratedMesh_Chunk_1";
				root.AddChild(source);

				var ctx = TerrainContinuationService.BuildContinueContext(root, "x+");
				if (ctx == null)
					throw new InvalidOperationException("BuildContinueContext returned null.");
				if (ctx.Direction != TerrainContinuationService.ContinueDirection.XPlus)
					throw new InvalidOperationException("Unexpected continuation direction.");
				if (ctx.FrontierSegments == null || ctx.FrontierSegments.Count != 1)
					throw new InvalidOperationException("Unexpected frontier segment count.");
				if (ctx.SuggestedResolution <= 0 || ctx.SourceLength <= 0 || ctx.SourceWidth <= 0)
					throw new InvalidOperationException("Continuation context has invalid sizing.");
				return $"frontier={ctx.FrontierSegments.Count}, res={ctx.SuggestedResolution}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"TC-2 Continuation seam constraint",
			"Проверяется ограничение по шву на новом меше continuation.",
			"ApplyEdgeConstraintToMesh должен менять seam-профиль без падений.",
			() =>
			{
				var root = new Node3D { Name = "ContinuationRoot" };
				var source = TestTools.CreateSampleTerrainInstance(24, 24, 12, 2);
				source.Name = "GeneratedMesh_Chunk_1";
				root.AddChild(source);

				var ctx = TerrainContinuationService.BuildContinueContext(root, "x+");

				var targetHeights = TestTools.CreateHeightGrid(12, 12, (x, z) => 20f + x * 0.4f + z * 0.2f);
				Mesh targetMesh = MeshBuilder.BuildTerrainMesh(targetHeights, 24, 24);
				if (targetMesh == null || targetMesh.GetSurfaceCount() == 0)
					throw new InvalidOperationException("Target mesh is invalid.");

				float before = GetSeamAverageY(targetMesh);
				TerrainContinuationService.ApplyEdgeConstraintToMesh(targetMesh, 12, ctx, false);
				float after = GetSeamAverageY(targetMesh);

				if (float.IsNaN(before) || float.IsNaN(after))
					throw new InvalidOperationException("Seam averages are invalid.");
				if (Math.Abs(after - before) < 0.01f)
					throw new InvalidOperationException("Seam constraint did not affect the mesh.");
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
		group.ActualResult = group.Passed ? $"Проверено {group.Operations.Count} операций" : "Есть проваленные операции";
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
