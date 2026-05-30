using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TerraGenerating.VisualStudioTests;

[TestClass]
public sealed class ProjectModuleMathTests
{
    [TestMethod]
    public void NM_1_Normalization()
    {
        var heights = new float[,] { { 2f, 4f }, { 6f, 8f } };
        TerrainMath.NormalizeToRange(heights, 0f, 1f);
        Assert.IsTrue(Math.Abs(heights[0, 0] - 0f) <= 0.001f, $"Expected min=0, got {heights[0, 0].ToString("0.###", CultureInfo.InvariantCulture)}");
        Assert.IsTrue(Math.Abs(heights[1, 1] - 1f) <= 0.001f, $"Expected max=1, got {heights[1, 1].ToString("0.###", CultureInfo.InvariantCulture)}");
    }

    [TestMethod]
    public void NM_2_BilinearSample()
    {
        var heights = new float[,] { { 0f, 2f }, { 2f, 4f } };
        float sample = TerrainMath.BilinearSample(heights, 0.5f, 0.5f);
        Assert.IsTrue(sample > 0.9f && sample < 3.1f, $"Unexpected bilinear sample: {sample.ToString("0.###", CultureInfo.InvariantCulture)}");
    }

    [TestMethod]
    public void NM_3_ResolutionModes()
    {
        int high = TerrainMath.ResolveResolution(1f, 0f, 1f, 0f, 0);
        int medium = TerrainMath.ResolveResolution(1f, 0f, 1f, 0f, 1);
        int adaptive = TerrainMath.ResolveResolution(1f, 0f, 1f, 0f, 2);

        Assert.AreEqual(50, high, "HighQuality mode should be 50");
        Assert.AreEqual(31, medium, "MediumQuality mode should be 31");
        Assert.IsTrue(adaptive > 0, "Adaptive mode should return positive resolution");
    }

    [TestMethod]
    public void NM_4_CoordinateTransforms()
    {
        Vector2 uv = TerrainMath.LonLatToUv(59.5, 30.5, 60f, 59f, 30f, 31f);
        Vector3 local = TerrainMath.UvToLocal(uv.X, uv.Y, 100f, 50f, 3f);

        Assert.IsTrue(uv.X >= 0f && uv.X <= 1f, $"uv.X is out of range: {uv.X.ToString("0.###", CultureInfo.InvariantCulture)}");
        Assert.IsTrue(uv.Y >= 0f && uv.Y <= 1f, $"uv.Y is out of range: {uv.Y.ToString("0.###", CultureInfo.InvariantCulture)}");
        Assert.AreEqual(3f, local.Y, 0.0001f, "Local Y should be preserved");
    }

    [TestMethod]
    public void NM_5_RoadMaskRasterization()
    {
        var mask = new float[32, 32];
        var polyline = new List<Vector2>
        {
            new Vector2(-8f, 0f),
            new Vector2(8f, 0f),
        };

        TerrainMath.RasterizeRoadMask(mask, polyline, 16, 16, 3f);

        float center = mask[16, 16];
        Assert.IsTrue(center > 0f, $"Road mask center is empty: {center.ToString("0.###", CultureInfo.InvariantCulture)}");
    }
}
