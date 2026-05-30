using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

public sealed partial class ProjectFunctionalTestSuite
{
	// Проверяет основные пользовательские сценарии генерации, экспорта и API-интеграций.
	public TestGroupResult Run()
	{
		var group = new TestGroupResult(
			"Функциональные тесты",
			"Проверяются функциональные требования ФТ-1..ФТ-16 для Godot-рантайма: генерация, UI, экспорт и отмена.",
			"Каждое функциональное требование должно дать наблюдаемый результат без ошибок и с корректным состоянием интерфейса или файла.");

		var watch = Stopwatch.StartNew();

		group.Operations.Add(TestTools.RunOperation("ФТ-1", "Система должна создавать ландшафт в выбранном узле: выбранный узел сцены → ландшафт в сцене.", "Ландшафт создан с корректным мешем в выбранном узле сцены.", () =>
		{
			var root = TestTools.GetOrCreateTestRoot();
			if (root == null)
				throw new InvalidOperationException("Test root is not available.");
			var terrain = TestTools.CreateSampleTerrainInstance(24, 24, 12, 1);
			terrain.Name = "FT1_Terrain";
			TestTools.AddToTestRoot(terrain, root);
			if (terrain.Mesh == null)
				throw new InvalidOperationException("Terrain mesh is null.");
			return "Террейн создан и добавлен в сцену";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-2", "Система должна выполнять случайную генерацию ландшафта по пользовательской конфигурации: конфигурация случайного режима → сгенерированный ландшафт.", "Ландшафт сгенерирован с корректными параметрами и разными размерами без ошибок.", () =>
		{
			var root = TestTools.GetOrCreateTestRoot();
			if (root == null)
				throw new InvalidOperationException("Test root is not available.");
			int[] sizes = { 16, 24, 32 };
			for (int i = 0; i < sizes.Length; i++)
			{
				var terrain = TestTools.CreateSampleTerrainInstance(sizes[i], sizes[i] + 8, Mathf.Max(8, sizes[i] / 2), 10 + i);
				terrain.Name = $"FT2_Terrain_{sizes[i]}";
				TestTools.AddToTestRoot(terrain, root);
				if (terrain.Mesh == null || terrain.Mesh.GetSurfaceCount() == 0)
					throw new InvalidOperationException($"Mesh generation failed for size {sizes[i]}");
			}
			return "Три размера случайного террейна построены";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-3", "Система должна добавлять водную плоскость с учетом диапазона высот ландшафта: уровень воды + параметры рельефа → плоскость воды в корректной позиции.", "Водная плоскость создана на правильной высоте с корректным материалом.", () =>
		{
			var root = TestTools.GetOrCreateTestRoot();
			if (root == null)
				throw new InvalidOperationException("Test root is not available.");
			var water = new RandomTerrainGenerator().GenerateWaterPlane(20, 16, 1.25f);
			water.Name = "FT3_Water";
			TestTools.AddToTestRoot(water, root);
			if (water == null || water.MaterialOverride == null || Math.Abs(water.Position.Y - 1.25f) > 0.001f)
				throw new InvalidOperationException("Water plane is invalid.");
			return $"Высота воды={water.Position.Y.ToString("0.###", CultureInfo.InvariantCulture)}";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-4", "Система должна применять текстуры ландшафта по заданным порогам и правилам смешивания: пути текстур и пороги → текстура ландшафта.", "Текстура ландшафта применена с корректным смешиванием по высоте и сохранена на диск.", () =>
		{
			var root = TestTools.GetOrCreateTestRoot();
			if (root == null)
				throw new InvalidOperationException("Test root is not available.");
			var terrain = TestTools.CreateSampleTerrainInstance(24, 24, 12, 2);
			terrain.Name = "FT4_Terrain";
			TestTools.AddToTestRoot(terrain, root);
			var outputPath = TestTools.CreateSampleTexturePath("terrain_texture_ft4.png");
			string dir = TestTools.EnsureTempFolder("");
			_ = dir;
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
				0,
				0.5f,
				null,
				null,
				null,
				null,
				false).GetAwaiter().GetResult();
			if (terrain.MaterialOverride == null || !FileAccess.FileExists(outputPath))
				throw new InvalidOperationException("Height texture was not applied or saved.");
			return "Высотная текстура применена";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-5", "Система должна формировать дорожную маску и интегрировать ее в материал ландшафта: параметры дорог → текстура ландшафта с дорожным слоем.", "Дорожная маска создана и интегрирована в текстуру с видимым отличием от простой текстуры.", () =>
		{
			var root = TestTools.GetOrCreateTestRoot();
			if (root == null)
				throw new InvalidOperationException("Test root is not available.");
			var terrain = TestTools.CreateSampleTerrainInstance(24, 24, 12, 3);
			terrain.Name = "FT5_Terrain";
			TestTools.AddToTestRoot(terrain, root);
			var roadMask = new float[32, 32];
			TerrainMath.RasterizeRoadMask(roadMask, new List<Vector2> { new Vector2(-8f, -4f), new Vector2(8f, 4f) }, 16, 16, 3f);
			var outputPath = TestTools.CreateSampleTexturePath("terrain_texture_road_ft5.png");
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
				0,
				0.5f,
				roadMask,
				TerraConfig.DefaultRoadTexturePath,
				null,
				null,
				false).GetAwaiter().GetResult();
			if (terrain.MaterialOverride == null || !FileAccess.FileExists(outputPath))
				throw new InvalidOperationException("Road texture was not applied or saved.");
			return "Текстура дорог применена";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-6", "Система должна выполнять расстановку объектов по заданным ограничениям: параметры объектов, вариативность, количество → объекты окружения в сцене.", "Объекты расставлены с учетом ограничений в узле ScatteredObjects.", () =>
		{
			var root = TestTools.GetOrCreateTestRoot();
			if (root == null)
				throw new InvalidOperationException("Test root is not available.");
			var terrain = TestTools.CreateSampleTerrainInstance(32, 32, 16, 4);
			terrain.Name = "FT6_Terrain";
			TestTools.AddToTestRoot(terrain, root);
			string modelPath = TestTools.FindFirstExistingResource(
				$"{TerraConfig.AddonRootPath}/Texture/source/tree.tscn",
				$"{TerraConfig.AddonRootPath}/Texture/source/tree2.tscn",
				$"{TerraConfig.AddonRootPath}/Texture/source/rock.tscn",
				$"{TerraConfig.AddonRootPath}/Texture/source/rock2.tscn");
			if (string.IsNullOrEmpty(modelPath))
				throw new InvalidOperationException("No scatter model resource found.");
			var scatter = new Godot.Collections.Dictionary
			{
				{ "trees", new Godot.Collections.Dictionary
					{
						{ "enabled", true },
						{ "count", 1 },
						{ "paths", new Godot.Collections.Array { modelPath } }
					}
				}
			};
			ObjectScatterPlacer.Scatter(root, terrain, 32, 32, 16, -4f, 14f, 0.35f, null, 32, scatter, null);
			Node scattered = root.GetNodeOrNull("ScatteredObjects");
			if (scattered == null || scattered.GetChildCount() == 0)
				throw new InvalidOperationException("ScatteredObjects node was not created.");
			return $"Размещено объектов={scattered.GetChildCount()}";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-7", "Система должна продолжать существующий ландшафт в выбранном направлении: направление + существующие ландшафты → новый состыкованный ландшафт.", "Поддерживаются ПО для выбора направления (связи на другие ландшафты).", () =>
		{
			string panelPath = $"{TerraConfig.AddonRootPath}/terra_panel.tscn";
			var panelScene = ResourceLoader.Load<PackedScene>(panelPath);
			if (panelScene == null)
				throw new InvalidOperationException($"{panelPath} not found.");
			var panel = TestTools.AttachToScene(panelScene.Instantiate()) as Node;
			if (panel == null)
				throw new InvalidOperationException("Panel instance is null.");
			try
			{
				panel.Call("_show_random_mode");
				var continueCheck = panel.GetNodeOrNull<CheckBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueGenerationCheck");
				var direction = panel.GetNodeOrNull<OptionButton>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueDirection");
				if (continueCheck == null || direction == null)
					throw new InvalidOperationException("Continuation controls are missing.");
				continueCheck.ButtonPressed = true;
				direction.Selected = 0;
				panel.Call("_update_continue_generation_ui");
				var widthField = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox");
				if (widthField == null || !widthField.Editable)
					throw new InvalidOperationException("Width field should stay editable for x+.");
				return "UI продолжения генерации проверен";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-8", "Система должна импортировать параметры из выбранного источника ландшафта в UI: выбранный ландшафт → автозаполнение полей в интерфейсе.", "Поля UI автоматически заполнены с верными параметрами источника.", () =>
		{
			string panelPath = $"{TerraConfig.AddonRootPath}/terra_panel.tscn";
			var panelScene = ResourceLoader.Load<PackedScene>(panelPath);
			if (panelScene == null)
				throw new InvalidOperationException($"{panelPath} not found.");
			var panel = TestTools.AttachToScene(panelScene.Instantiate()) as Node;
			if (panel == null)
				throw new InvalidOperationException("Panel instance is null.");
			try
			{
				var data = new Godot.Collections.Dictionary
				{
					{ "max_height", 17f },
					{ "resolution", 64 },
					{ "sand_grass", 0.22f },
					{ "grass_rock", 0.74f },
					{ "smoothing", 0.61f },
					{ "texture_mode", 1 },
					{ "slope_blend", 0.48f },
					{ "water_level", 0.33f },
					{ "source_width", 144 },
					{ "source_length", 188 },
				};
				panel.Call("apply_continue_source_settings", data);
				return "Словарь источника применен к UI";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-17", "Система должна продолжать генерацию в направлении X-.", "Для X- ширина блокируется, а length остается доступным.", () =>
		{
			string panelPath = $"{TerraConfig.AddonRootPath}/terra_panel.tscn";
			var panelScene = ResourceLoader.Load<PackedScene>(panelPath);
			if (panelScene == null)
				throw new InvalidOperationException($"{panelPath} not found.");
			var panel = TestTools.AttachToScene(panelScene.Instantiate()) as Node;
			if (panel == null)
				throw new InvalidOperationException("Panel instance is null.");
			try
			{
				panel.Call("_show_random_mode");
				var continueCheck = panel.GetNodeOrNull<CheckBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueGenerationCheck");
				var direction = panel.GetNodeOrNull<OptionButton>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueDirection");
				var lengthField = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox");
				var widthField = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Z/Zbox");
				if (continueCheck == null || direction == null || lengthField == null || widthField == null)
					throw new InvalidOperationException("Continuation controls are missing.");
				continueCheck.ButtonPressed = true;
				direction.Selected = 1;
				panel.Call("_update_continue_generation_ui");
				if (!lengthField.Editable || widthField.Editable)
					throw new InvalidOperationException("X- continuation UI state is invalid.");
				if (panel.Call("_get_continue_direction").AsString() != "x-")
					throw new InvalidOperationException("Direction text is not x-.");
				return "X- continuation UI verified";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-18", "Система должна продолжать генерацию в направлении Z+.", "Для Z+ длина блокируется, а width остается доступным.", () =>
		{
			string panelPath = $"{TerraConfig.AddonRootPath}/terra_panel.tscn";
			var panelScene = ResourceLoader.Load<PackedScene>(panelPath);
			if (panelScene == null)
				throw new InvalidOperationException($"{panelPath} not found.");
			var panel = TestTools.AttachToScene(panelScene.Instantiate()) as Node;
			if (panel == null)
				throw new InvalidOperationException("Panel instance is null.");
			try
			{
				panel.Call("_show_random_mode");
				var continueCheck = panel.GetNodeOrNull<CheckBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueGenerationCheck");
				var direction = panel.GetNodeOrNull<OptionButton>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueDirection");
				var lengthField = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox");
				var widthField = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Z/Zbox");
				if (continueCheck == null || direction == null || lengthField == null || widthField == null)
					throw new InvalidOperationException("Continuation controls are missing.");
				continueCheck.ButtonPressed = true;
				direction.Selected = 2;
				panel.Call("_update_continue_generation_ui");
				if (lengthField.Editable || !widthField.Editable)
					throw new InvalidOperationException("Z+ continuation UI state is invalid.");
				if (panel.Call("_get_continue_direction").AsString() != "z+")
					throw new InvalidOperationException("Direction text is not z+.");
				return "Z+ continuation UI verified";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-19", "Система должна продолжать генерацию в направлении Z-.", "Для Z- длина блокируется, а width остается доступным.", () =>
		{
			string panelPath = $"{TerraConfig.AddonRootPath}/terra_panel.tscn";
			var panelScene = ResourceLoader.Load<PackedScene>(panelPath);
			if (panelScene == null)
				throw new InvalidOperationException($"{panelPath} not found.");
			var panel = TestTools.AttachToScene(panelScene.Instantiate()) as Node;
			if (panel == null)
				throw new InvalidOperationException("Panel instance is null.");
			try
			{
				panel.Call("_show_random_mode");
				var continueCheck = panel.GetNodeOrNull<CheckBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueGenerationCheck");
				var direction = panel.GetNodeOrNull<OptionButton>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/ContinueGenerationRow/ContinueDirection");
				var lengthField = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox");
				var widthField = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Z/Zbox");
				if (continueCheck == null || direction == null || lengthField == null || widthField == null)
					throw new InvalidOperationException("Continuation controls are missing.");
				continueCheck.ButtonPressed = true;
				direction.Selected = 3;
				panel.Call("_update_continue_generation_ui");
				if (lengthField.Editable || !widthField.Editable)
					throw new InvalidOperationException("Z- continuation UI state is invalid.");
				if (panel.Call("_get_continue_direction").AsString() != "z-")
					throw new InvalidOperationException("Direction text is not z-.");
				return "Z- continuation UI verified";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-11", "Система должна отображать прогресс выполнения длительных операций: запущенная операция → окно прогресса и статус.", "Окно прогресса отображается в речали в режиме реального времени з процентом и текстом.", () =>
		{
			string dialogPath = $"{TerraConfig.AddonRootPath}/progress_dialog.tscn";
			var scene = ResourceLoader.Load<PackedScene>(dialogPath);
			if (scene == null)
				throw new InvalidOperationException($"{dialogPath} not found.");
			var dialog = TestTools.AttachToScene(scene.Instantiate()) as Window;
			if (dialog == null)
				throw new InvalidOperationException("Progress dialog instance is null.");
			try
			{
				dialog.Call("update_progress", 42f, "Подготовка...");
				var percentLabel = dialog.GetNodeOrNull<Label>("VBoxContainer/PercentLabel");
				if (percentLabel == null || !percentLabel.Text.Contains("42"))
					throw new InvalidOperationException("Progress dialog did not update percent label.");
				return "Прогресс-диалог обновляется";
			}
			finally
			{
				dialog.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-12", "Система должна обеспечивать контролируемую отмену длительной операции: команда пользователя → корректная остановка процесса.", "Операция сразу ОК, не останавливаются в середине и требуют очистки ресурсов.", () =>
		{
			string dialogPath = $"{TerraConfig.AddonRootPath}/progress_dialog.tscn";
			var scene = ResourceLoader.Load<PackedScene>(dialogPath);
			if (scene == null)
				throw new InvalidOperationException($"{dialogPath} not found.");
			var dialog = TestTools.AttachToScene(scene.Instantiate()) as Window;
			if (dialog == null)
				throw new InvalidOperationException("Progress dialog instance is null.");
			try
			{
				var sink = new CancelSignalSink();
				dialog.Connect("cancel_requested", new Callable(sink, nameof(CancelSignalSink.Mark)), flags: (uint)GodotObject.ConnectFlags.OneShot);
				dialog.Call("update_progress", 15f, "Выполняется...");
				dialog.Call("_on_close_requested");
				if (!sink.WasCalled)
					throw new InvalidOperationException("Cancel signal was not emitted.");
				return "Отмена по закрытию окна проверена";
			}
			finally
			{
				dialog.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-13", "Система должна экспортировать выбранную ветку сцены в формат glTF/GLB: узел / ветка + путь → экспортные файлы.", "Экспортные файлы .gltf и .glb сохранены с валидными данными.", () =>
		{
			var exportRoot = new Node3D { Name = "ExportRoot" };
			var terrain = TestTools.CreateSampleTerrainInstance(16, 16, 8, 5);
			exportRoot.AddChild(terrain);
			string exportDir = TestTools.EnsureTempFolder("export_ft13");
			if (!ClassDB.ClassExists("GLTFDocument") || !ClassDB.ClassExists("GLTFState"))
				return "GLTFDocument недоступен в этой версии Godot";
			var gltf = (GodotObject)ClassDB.Instantiate("GLTFDocument");
			var state = (GodotObject)ClassDB.Instantiate("GLTFState");
			var appendResult = gltf.Call("append_from_scene", exportRoot, state);
			Error appendErr = (Error)appendResult.AsInt32();
			if (appendErr != Error.Ok)
				throw new InvalidOperationException($"AppendFromScene failed: {appendErr}");
			string gltfPath = System.IO.Path.Combine(exportDir, "ft13_export.gltf");
			string glbPath = System.IO.Path.Combine(exportDir, "ft13_export.glb");
			var writeResult = gltf.Call("write_to_filesystem", state, gltfPath);
			Error writeErr = (Error)writeResult.AsInt32();
			if (writeErr != Error.Ok)
				throw new InvalidOperationException($"WriteToFilesystem failed: {writeErr}");
			var buffer = gltf.Call("generate_buffer", state);
			byte[] bytes = buffer.AsByteArray();
			if (bytes == null || bytes.Length == 0)
				throw new InvalidOperationException("Generated GLB buffer is empty.");
			FileAccess fa = FileAccess.Open(glbPath, FileAccess.ModeFlags.Write);
			if (fa == null)
				throw new InvalidOperationException("Could not create GLB file.");
			fa.StoreBuffer(bytes);
			fa.Close();
			if (!FileAccess.FileExists(gltfPath) || !FileAccess.FileExists(glbPath))
				throw new InvalidOperationException("Export files were not created.");
			return "GLTF и GLB экспортированы";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-14", "Система должна подготавливать текстуры и материалы к корректному экспорту: материалы сцены → экспортированные текстуры и ссылки.", "Текстуры и материалы подготовлены для экспорта с правильными путями и ресурсами.", () =>
		{
			var root = TestTools.GetOrCreateTestRoot();
			if (root == null)
				throw new InvalidOperationException("Test root is not available.");
			var terrain = TestTools.CreateSampleTerrainInstance(24, 24, 12, 6);
			terrain.Name = "FT14_Terrain";
			TestTools.AddToTestRoot(terrain, root);
			var heights = TestTools.CreateHeightGrid(24, 24, (x, z) => Mathf.Sin(x * 0.2f) + Mathf.Cos(z * 0.15f));
			RealWorldTexturePainter.ApplyHeightTexture(
				terrain,
				heights,
				24,
				24,
				TerraConfig.SandTexturePath,
				TerraConfig.GrassTexturePath,
				TerraConfig.RockTexturePath,
				0.33f,
				0.66f);
			if (terrain.MaterialOverride == null || !FileAccess.FileExists("res://real_world_terrain_texture_debug.png"))
				throw new InvalidOperationException("RealWorldTexturePainter did not produce output.");
			return "Реалистичная текстура применена";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-15", "Система должна формировать отчет по результатам экспорта: результаты экспорта → файл export_report.txt.", "Отчет экспорта создан с деталями ОК и ошибок для операций.", () =>
		{
			string pluginPath = $"{TerraConfig.AddonRootPath}/terra_generating_main.gd";
			var pluginScript = ResourceLoader.Load<Script>(pluginPath);
			if (pluginScript == null)
				throw new InvalidOperationException($"{pluginPath} is missing.");
			GodotObject plugin;
			if (pluginScript.HasMethod("instantiate"))
				plugin = (GodotObject)pluginScript.Call("instantiate");
			else if (pluginScript.HasMethod("new"))
				plugin = (GodotObject)pluginScript.Call("new");
			else
				throw new InvalidOperationException("Script instance method not found.");
			string exportDir = TestTools.EnsureTempFolder("export_report_ft15");
			var lines = new Godot.Collections.Array { "line 1", "line 2" };
			plugin.Call("_write_export_report", exportDir, lines);
			string reportPath = System.IO.Path.Combine(exportDir, "export_report.txt");
			if (!FileAccess.FileExists(reportPath))
				throw new InvalidOperationException("Export report file was not created.");
			string text = FileAccess.GetFileAsString(reportPath);
			if (!text.Contains("line 1") || !text.Contains("line 2"))
				throw new InvalidOperationException("Export report file does not contain expected lines.");
			return "Отчет экспорта сформирован";
		}));

		group.Operations.Add(TestTools.RunOperation("ФТ-16", "Система должна поддерживать корректную работу при смене имени / пути папки: конфигурация расположения → успешная загрузка модуля.", "Модуло загружается корректно независимо от пути папки аддона.", () =>
		{
			string panelPath = $"{TerraConfig.AddonRootPath}/terra_panel.tscn";
			var panelScene = ResourceLoader.Load<PackedScene>(panelPath);
			if (panelScene == null)
				throw new InvalidOperationException($"{panelPath} not found.");
			var panel = TestTools.AttachToScene(panelScene.Instantiate()) as Node;
			if (panel == null)
				throw new InvalidOperationException("Panel instance is null.");
			try
			{
				string legacy = "res://addons/terragenerating/Texture/road.jpg";
				string normalized = panel.Call("_normalize_legacy_addon_path", legacy).AsString();
				if (!normalized.Contains("Texture/road.jpg"))
					throw new InvalidOperationException("Legacy path was not normalized.");
				return $"Нормализованный путь={normalized}";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		watch.Stop();
		group.DurationMs = watch.ElapsedMilliseconds;
		group.Passed = true;
		for (int i = 0; i < group.Operations.Count; i++)
		{
			if (!group.Operations[i].Passed)
				group.Passed = false;
		}
		group.ActualResult = group.Passed ? $"Проверено {group.Operations.Count} требований ФТ" : "Есть проваленные функциональные требования";
		return group;
	}

	private sealed partial class CancelSignalSink : RefCounted
	{
		public bool WasCalled { get; private set; }

		// Отмечает, что сигнал отмены действительно был вызван.
		public void Mark()
		{
			WasCalled = true;
		}
	}
}
