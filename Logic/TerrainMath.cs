using Godot;
using System;
using System.Collections.Generic;

public static class TerrainMath
{
	public enum ResolutionMode
	{
		HighQuality = 0,
		MediumQuality = 1,
		Adaptive = 2
	}

	public static void NormalizeToRange(float[,] h, float minTarget, float maxTarget)
	{
		GetMinMax(h, out float min, out float max);
		if (Math.Abs(max - min) < 0.001f)
		{
			for (int x = 0; x < h.GetLength(0); x++)
			{
				for (int z = 0; z < h.GetLength(1); z++)
				{
					h[x, z] = minTarget;
				}
			}
			return;
		}

		for (int x = 0; x < h.GetLength(0); x++)
		{
			for (int z = 0; z < h.GetLength(1); z++)
			{
				float t = Mathf.InverseLerp(min, max, h[x, z]);
				h[x, z] = Mathf.Lerp(minTarget, maxTarget, t);
			}
		}
	}

	public static float BilinearSample(float[,] h, float fx, float fz)
	{
		int resX = h.GetLength(0);
		int resZ = h.GetLength(1);
		fx = Mathf.Clamp(fx, 0, resX - 1);
		fz = Mathf.Clamp(fz, 0, resZ - 1);

		int x0 = Mathf.FloorToInt(fx);
		int z0 = Mathf.FloorToInt(fz);
		int x1 = Mathf.Min(x0 + 1, resX - 1);
		int z1 = Mathf.Min(z0 + 1, resZ - 1);

		float tx = fx - x0;
		float tz = fz - z0;

		float h00 = h[x0, z0];
		float h10 = h[x1, z0];
		float h01 = h[x0, z1];
		float h11 = h[x1, z1];

		return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
	}

	public static int ResolveResolution(float north, float south, float west, float east, int resolutionMode)
	{
		float dLat = Math.Abs(north - south);
		float dLon = Math.Abs(east - west);
		float meanLatRad = Mathf.DegToRad((north + south) * 0.5f);
		const float METERS_PER_DEGREE_LAT = 111320f;
		float metersPerDegLon = Mathf.Cos(meanLatRad) * METERS_PER_DEGREE_LAT;
		float widthMeters = dLon * metersPerDegLon;
		float depthMeters = dLat * METERS_PER_DEGREE_LAT;
		float maxSideKm = Mathf.Max(widthMeters, depthMeters) / 1000f;

		ResolutionMode mode = (ResolutionMode)resolutionMode;
		return mode switch
		{
			ResolutionMode.HighQuality => 50,
			ResolutionMode.MediumQuality => 31,
			_ => maxSideKm <= 2f ? 50
				: maxSideKm <= 5f ? 45
				: maxSideKm <= 10f ? 39
				: maxSideKm <= 20f ? 33
				: 27
		};
	}

	public static Vector2 LonLatToUv(double lat, double lon, float north, float south, float west, float east)
	{
		float u = (float)((lon - west) / (east - west));
		float v = (float)((north - lat) / (north - south));
		return new Vector2(u, v);
	}

	public static Vector3 UvToLocal(float u, float v, float widthUnits, float depthUnits, float y)
	{
		float halfX = widthUnits * 0.5f;
		float halfZ = depthUnits * 0.5f;
		float lx = u * widthUnits - halfX;
		float lz = halfZ - v * depthUnits;
		return new Vector3(lx, y, lz);
	}

	public static void RasterizeRoadMask(float[,] mask, List<Vector2> polylineWorldXZ, int length, int width, float roadWidth)
	{
		if (mask == null || polylineWorldXZ == null || polylineWorldXZ.Count < 2)
			return;

		int texResX = mask.GetLength(0);
		int texResZ = mask.GetLength(1);
		float halfLength = length * 0.5f;
		float halfWidth = width * 0.5f;

		for (int i = 0; i < polylineWorldXZ.Count - 1; i++)
		{
			Vector2 p1 = polylineWorldXZ[i];
			Vector2 p2 = polylineWorldXZ[i + 1];
			float segmentLength = p1.DistanceTo(p2);
			int numSteps = Mathf.Max(10, (int)(segmentLength * Mathf.Max(texResX, texResZ) / Mathf.Max(length, width)));

			for (int step = 0; step <= numSteps; step++)
			{
				float t = (float)step / numSteps;
				float x = Mathf.Lerp(p1.X, p2.X, t);
				float z = Mathf.Lerp(p1.Y, p2.Y, t);

				float normalizedX = (x + halfLength) / length;
				float normalizedZ = (z + halfWidth) / width;
				int texX = (int)(normalizedX * (texResX - 1));
				int texZ = (int)(normalizedZ * (texResZ - 1));

				float roadWidthInTex = roadWidth * Mathf.Max(texResX, texResZ) / Mathf.Max(length, width);
				int roadRadius = Mathf.Max(1, (int)(roadWidthInTex * 0.5f));

				for (int dz = -roadRadius; dz <= roadRadius; dz++)
				{
					for (int dx = -roadRadius; dx <= roadRadius; dx++)
					{
						int px = texX + dx;
						int pz = texZ + dz;
						if (px < 0 || px >= texResX || pz < 0 || pz >= texResZ) continue;

						float dist = Mathf.Sqrt(dx * dx + dz * dz);
						float maskValue = 1.0f - Mathf.Clamp(dist / roadRadius, 0.0f, 1.0f);
						maskValue = Mathf.SmoothStep(0.0f, 1.0f, maskValue);
						mask[px, pz] = Mathf.Max(mask[px, pz], maskValue);
					}
				}
			}
		}
	}

	private static void GetMinMax(float[,] h, out float min, out float max)
	{
		min = float.MaxValue;
		max = float.MinValue;
		for (int z = 0; z < h.GetLength(1); z++)
		{
			for (int x = 0; x < h.GetLength(0); x++)
			{
				float v = h[x, z];
				if (v < min) min = v;
				if (v > max) max = v;
			}
		}
	}
}
