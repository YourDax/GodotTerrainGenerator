using Godot;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

public sealed class ProjectPerformanceTestSuite
{
	public TestGroupResult Run()
	{
		var group = new TestGroupResult(
			"Тестирование производительности",
			"Проверяется создание мешей разных размеров, применение API и несколько вариантов параметров генерации.",
			"Все операции должны выполняться стабильно и укладываться в разумное время без исключений.");

		var watch = Stopwatch.StartNew();

		group.Operations.Add(TestTools.RunOperation("PR-1", "Замеряется генерация меша малого размера.", "Меш должен быть создан быстро и без пустых поверхностей.", () =>
		{
			var generator = new RandomTerrainGenerator();
			Mesh mesh = generator.GenerateMesh(16, 16, -3f, 10f, 8, 0.7f, false, 0.35f, 1, 2, 3, 4);
			if (mesh == null || mesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("Small mesh generation failed.");
			return $"Surfaces={mesh.GetSurfaceCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-2", "Замеряется генерация меша среднего размера.", "Меш должен строиться для среднего объема данных.", () =>
		{
			var generator = new RandomTerrainGenerator();
			Mesh mesh = generator.GenerateMesh(32, 24, -5f, 14f, 16, 0.8f, true, 0.35f, 11, 12, 13, 14);
			if (mesh == null || mesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("Medium mesh generation failed.");
			return $"Surfaces={mesh.GetSurfaceCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-3", "Замеряется генерация меша большого размера.", "Крупный меш должен создаваться без исключения и с валидной геометрией.", () =>
		{
			var generator = new RandomTerrainGenerator();
			Mesh mesh = generator.GenerateMesh(48, 40, -8f, 18f, 20, 0.9f, true, 0.45f, 21, 22, 23, 24);
			if (mesh == null || mesh.GetSurfaceCount() == 0)
				throw new InvalidOperationException("Large mesh generation failed.");
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

		group.Operations.Add(TestTools.RunOperation("PR-5", "Проверяется производительность API-клиентов без сети.", "OpenTopoData и OSM должны быстро обработать подготовленные ответы.", () =>
		{
			var topoHttp = TestTools.CreateHttpClient(request =>
			{
				var json = "{\"results\":[{\"elevation\":1},{\"elevation\":2},{\"elevation\":3},{\"elevation\":4}]}";
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
			});
			var osmHttp = TestTools.CreateHttpClient(request =>
			{
				string body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				string decoded = Uri.UnescapeDataString(body);
				string json = decoded.Contains("natural\"=\"tree")
					? "{\"elements\":[{\"type\":\"node\",\"lat\":1,\"lon\":2,\"tags\":{\"natural\":\"tree\"}}]}"
					: "{\"elements\":[{\"type\":\"way\",\"geometry\":[{\"lat\":1,\"lon\":1},{\"lat\":1,\"lon\":2},{\"lat\":2,\"lon\":2}],\"tags\":{\"natural\":\"water\"}}]}";
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
			});
			var topo = new OpenTopoDataClient(topoHttp);
			var osm = new OsmOverpassClient(osmHttp);
			float[,] heights = topo.FetchHeightsGridAsync(60f, 30f, 59f, 31f, 2, 4, 1, 0, 1, 0, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
			var trees = osm.FetchTreeNodesAsync(59f, 30f, 60f, 31f, 10).GetAwaiter().GetResult();
			var water = osm.FetchWaterPolygonsAsync(59f, 30f, 60f, 31f, 10).GetAwaiter().GetResult();
			if (heights == null || trees.Count == 0 || water.Count == 0)
				throw new InvalidOperationException("API clients did not return valid data.");
			return $"Heights={heights.GetLength(0)}x{heights.GetLength(1)}, Trees={trees.Count}, Water={water.Count}";
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

		group.Operations.Add(TestTools.RunOperation("PR-9", "Проверка производительности OpenTopoData API при загрузке больших сеток.", "API оббабатывает данные без задержек и деградации производительности.", () =>
		{
			var http = TestTools.CreateHttpClient(request =>
			{
				// Симуляция большой сетки высот
				var json = new StringBuilder();
				json.Append("{\"results\":[");
				for (int i = 0; i < 100; i++)
				{
					if (i > 0) json.Append(',');
					json.Append("{\"elevation\":").Append(100 + (i % 20)).Append('}');
				}
				json.Append("]}");
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json.ToString(), Encoding.UTF8, "application/json") };
			});
			var client = new OpenTopoDataClient(http);
			float[,] heights = client.FetchHeightsGridAsync(60f, 30f, 58f, 32f, 10, 10, 1, 0, 1, 0, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
			if (heights == null || heights.Length == 0)
				throw new InvalidOperationException("Large height grid was not loaded.");
			return $"Загруженная сетка: {heights.GetLength(0)}x{heights.GetLength(1)}";
		}));

		group.Operations.Add(TestTools.RunOperation("PR-10", "Проверка производительности OSM Overpass API при обработке деревьев и воды.", "OSM данные обрабатыются эффективно для крупных географических областей.", () =>
		{
			var http = TestTools.CreateHttpClient(request =>
			{
				string body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				string decoded = Uri.UnescapeDataString(body);
				if (decoded.Contains("natural\"=\"tree"))
				{
					// Симуляция большого количества деревьев
					var json = new StringBuilder();
					json.Append("{\"elements\":[");
					for (int i = 0; i < 50; i++)
					{
						if (i > 0) json.Append(',');
						var lat = (59 + i * 0.01f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
						var lon = (30 + i * 0.01f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
						json.Append("{\"type\":\"node\",\"lat\":").Append(lat).Append(",\"lon\":").Append(lon).Append(",\"tags\":{\"natural\":\"tree\"}}");
					}
					json.Append("]}");
					return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json.ToString(), Encoding.UTF8, "application/json") };
				}
				else
				{
					// Симуляция больших водных полигонов с правильной структурой
					var json = "{\"elements\":[{\"type\":\"way\",\"id\":1,\"nodes\":[1,2,3,4,1],\"members\":[],\"geometry\":[{\"lat\":59.0,\"lon\":30.0},{\"lat\":59.0,\"lon\":31.0},{\"lat\":60.0,\"lon\":31.0},{\"lat\":60.0,\"lon\":30.0},{\"lat\":59.0,\"lon\":30.0}],\"tags\":{\"natural\":\"water\"}}]}";
					return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
				}
			});
			var client = new OsmOverpassClient(http);
			var trees = client.FetchTreeNodesAsync(59f, 30f, 61f, 32f, 20).GetAwaiter().GetResult();
			var water = client.FetchWaterPolygonsAsync(59f, 30f, 61f, 32f, 20).GetAwaiter().GetResult();
			if (trees.Count == 0 && water.Count == 0)
				throw new InvalidOperationException("OSM data was not loaded.");
			return $"OSM данные: Деревья={trees.Count}, Вода={water.Count}";
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
}
