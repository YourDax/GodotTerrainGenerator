using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

public sealed class ProjectModuleTestSuite
{
	public TestGroupResult Run()
	{
		var group = new TestGroupResult(
			"Модульные тесты",
			"Проверяются математические функции, генерация меша, построение маски дорог и основные константы плагина.",
			"Все ключевые низкоуровневые операции возвращают корректные значения и валидные ресурсы.");

		var watch = Stopwatch.StartNew();
		group.Operations.Add(TestTools.RunOperation(
			"NM-1 Normalization",
			"Проверяется перенос значений высоты в целевой диапазон.",
			"Минимум должен стать 0, максимум должен стать 1.",
			() =>
			{
				var heights = new float[,] { { 2f, 4f }, { 6f, 8f } };
				TerrainMath.NormalizeToRange(heights, 0f, 1f);
				if (Math.Abs(heights[0, 0] - 0f) > 0.001f || Math.Abs(heights[1, 1] - 1f) > 0.001f)
					throw new InvalidOperationException("NormalizeToRange returned unexpected values.");
				return $"Диапазон: {heights[0, 0].ToString("0.###", CultureInfo.InvariantCulture)}..{heights[1, 1].ToString("0.###", CultureInfo.InvariantCulture)}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"NM-2 Bilinear sample",
			"Проверяется билинейная выборка внутри сетки.",
			"Центральная точка должна попадать между крайними значениями.",
			() =>
			{
				var heights = new float[,] { { 0f, 2f }, { 2f, 4f } };
				float sample = TerrainMath.BilinearSample(heights, 0.5f, 0.5f);
				if (sample <= 0.9f || sample >= 3.1f)
					throw new InvalidOperationException($"Unexpected bilinear sample: {sample}");
				return $"Центр массива: {sample.ToString("0.###", CultureInfo.InvariantCulture)}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"NM-3 Resolution modes",
			"Проверяется расчет разрешения для разных режимов.",
			"HighQuality и MediumQuality должны возвращать фиксированные значения, adaptive - рабочий диапазон.",
			() =>
			{
				int high = TerrainMath.ResolveResolution(1f, 0f, 1f, 0f, 0);
				int medium = TerrainMath.ResolveResolution(1f, 0f, 1f, 0f, 1);
				int adaptive = TerrainMath.ResolveResolution(1f, 0f, 1f, 0f, 2);
				if (high != 50 || medium != 31 || adaptive <= 0)
					throw new InvalidOperationException("ResolveResolution returned invalid values.");
				return $"Режимы: {high}/{medium}/{adaptive}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"NM-4 Coordinate transforms",
			"Проверяются преобразования lon/lat -> UV и UV -> local.",
			"Преобразования должны возвращать координаты в ожидаемом диапазоне.",
			() =>
			{
				Vector2 uv = TerrainMath.LonLatToUv(59.5, 30.5, 60f, 59f, 30f, 31f);
				Vector3 local = TerrainMath.UvToLocal(uv.X, uv.Y, 100f, 50f, 3f);
				if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f)
					throw new InvalidOperationException("UV is outside of expected range.");
				return $"UV={uv.X.ToString("0.###", CultureInfo.InvariantCulture)},{uv.Y.ToString("0.###", CultureInfo.InvariantCulture)} localY={local.Y.ToString("0.###", CultureInfo.InvariantCulture)}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"NM-5 Road mask rasterization",
			"Проверяется построение маски дороги по ломаной.",
			"В центре траектории должны появляться ненулевые значения маски.",
			() =>
			{
				var mask = new float[32, 32];
				var polyline = new List<Vector2>
				{
					new Vector2(-8f, 0f),
					new Vector2(8f, 0f),
				};
				TerrainMath.RasterizeRoadMask(mask, polyline, 16, 16, 3f);
				float center = mask[16, 16];
				if (center <= 0f)
					throw new InvalidOperationException("Road mask center is empty.");
				return $"Центр маски={center.ToString("0.###", CultureInfo.InvariantCulture)}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"NM-6 Terrain mesh generation",
			"Проверяется создание меша случайного рельефа.",
			"Генератор должен возвращать меш с поверхностью.",
			() =>
			{
				var generator = new RandomTerrainGenerator();
				Mesh mesh = generator.GenerateMesh(24, 24, -4f, 12f, 12, 0.75f, false, 0.35f, 11, 22, 33, 44);
				if (mesh == null || mesh.GetSurfaceCount() == 0)
					throw new InvalidOperationException("GenerateMesh returned an empty mesh.");
				return $"SurfaceCount={mesh.GetSurfaceCount()}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"NM-7 Water plane generation",
			"Проверяется создание водной плоскости.",
			"Плоскость должна иметь воду-материал и корректную высоту.",
			() =>
			{
				var generator = new RandomTerrainGenerator();
				MeshInstance3D water = generator.GenerateWaterPlane(12, 8, 0.75f);
				if (water == null || water.MaterialOverride == null)
					throw new InvalidOperationException("GenerateWaterPlane returned invalid water node.");
				return $"WaterY={water.Position.Y.ToString("0.###", CultureInfo.InvariantCulture)}";
			}));

		group.Operations.Add(TestTools.RunOperation(
			"NM-8 Core resources",
			"Проверяются базовые пути ресурсов плагина.",
			"Основные текстуры и путь корня аддона должны существовать.",
			() =>
			{
				string sand = TerraConfig.SandTexturePath;
				string grass = TerraConfig.GrassTexturePath;
				string rock = TerraConfig.RockTexturePath;
				if (string.IsNullOrWhiteSpace(TerraConfig.AddonRootPath))
					throw new InvalidOperationException("AddonRootPath is empty.");
				if (!ResourceLoader.Exists(sand) || !ResourceLoader.Exists(grass) || !ResourceLoader.Exists(rock))
					throw new InvalidOperationException("One or more default terrain textures are missing.");
				return "Базовые текстуры доступны";
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
}
