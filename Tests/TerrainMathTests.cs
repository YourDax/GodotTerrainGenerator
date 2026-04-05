using Godot;
using System;
using System.Collections.Generic;

public static class TerrainMathTests
{
	public static bool RunAll()
	{
		try
		{
			TestNormalizeToRange();
			TestBilinearInterpolation();
			TestResolveResolution();
			TestRoadMaskRasterization();
			TestOsmCoordinateTransforms();
			GD.Print("[Tests] TerrainMathTests: all tests passed");
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Tests] TerrainMathTests failed: {ex.Message}");
			return false;
		}
	}

	private static void TestNormalizeToRange()
	{
		float[,] h =
		{
			{ 10f, 20f },
			{ 30f, 40f }
		};

		TerrainMath.NormalizeToRange(h, 0f, 1f);
		AssertNear(h[0, 0], 0f, 0.0001f, "Normalize min");
		AssertNear(h[1, 1], 1f, 0.0001f, "Normalize max");
	}

	private static void TestBilinearInterpolation()
	{
		float[,] h =
		{
			{ 0f, 10f },
			{ 10f, 20f }
		};

		float center = TerrainMath.BilinearSample(h, 0.5f, 0.5f);
		AssertNear(center, 10f, 0.0001f, "Bilinear center");
	}

	private static void TestResolveResolution()
	{
		int high = TerrainMath.ResolveResolution(55.8f, 55.7f, 37.5f, 37.6f, 0);
		int medium = TerrainMath.ResolveResolution(55.8f, 55.7f, 37.5f, 37.6f, 1);
		int adaptiveSmall = TerrainMath.ResolveResolution(55.800f, 55.790f, 37.500f, 37.510f, 2);
		int adaptiveLarge = TerrainMath.ResolveResolution(60.0f, 50.0f, 20.0f, 50.0f, 2);

		AssertEqual(high, 50, "Resolution high");
		AssertEqual(medium, 31, "Resolution medium");
		AssertEqual(adaptiveSmall, 50, "Resolution adaptive small bbox");
		AssertEqual(adaptiveLarge, 27, "Resolution adaptive large bbox");
	}

	private static void TestRoadMaskRasterization()
	{
		float[,] mask = new float[64, 64];
		var path = new List<Vector2>
		{
			new Vector2(-20f, -20f),
			new Vector2(20f, 20f)
		};

		TerrainMath.RasterizeRoadMask(mask, path, 100, 100, 4f);
		float center = mask[32, 32];
		if (center <= 0f)
		{
			throw new Exception("Road mask center must be painted");
		}
	}

	private static void TestOsmCoordinateTransforms()
	{
		Vector2 uv = TerrainMath.LonLatToUv(55.75, 37.60, 56f, 55f, 37f, 38f);
		AssertNear(uv.X, 0.6f, 0.0001f, "UV u");
		AssertNear(uv.Y, 0.25f, 0.0001f, "UV v");

		Vector3 local = TerrainMath.UvToLocal(0.5f, 0.5f, 100f, 200f, 12f);
		AssertNear(local.X, 0f, 0.0001f, "Local center x");
		AssertNear(local.Z, 0f, 0.0001f, "Local center z");
		AssertNear(local.Y, 12f, 0.0001f, "Local y passthrough");
	}

	private static void AssertNear(float actual, float expected, float eps, string name)
	{
		if (Mathf.Abs(actual - expected) > eps)
		{
			throw new Exception($"{name}: expected {expected}, got {actual}");
		}
	}

	private static void AssertEqual(int actual, int expected, string name)
	{
		if (actual != expected)
		{
			throw new Exception($"{name}: expected {expected}, got {actual}");
		}
	}
}
