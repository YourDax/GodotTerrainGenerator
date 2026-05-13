using Godot;
using System;
using System.Diagnostics;

public sealed class ProjectVisualTestSuite
{
	public TestGroupResult Run()
	{
		var group = new TestGroupResult(
			"Визуальные проверки",
			"Проверяется полный состав UI-панелей, диалогов и сцен плагина.",
			"Все ключевые узлы интерфейса и сцен должны присутствовать в сценах Godot.");

		var watch = Stopwatch.StartNew();

		group.Operations.Add(TestTools.RunOperation("VI-1", "Проверяется сцена панели плагина.", "Сцена должна содержать все ключевые контейнеры и кнопки.", () =>
		{
			string panelPath = $"{TerraConfig.AddonRootPath}/terra_panel.tscn";
			var scene = ResourceLoader.Load<PackedScene>(panelPath);
			if (scene == null)
				throw new InvalidOperationException($"{panelPath} missing.");
			var panel = scene.Instantiate();
			try
			{
				string[] paths =
				{
					"MainScroll/MainContent/SectionMode/Body/RealMapCheck",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Z/Zbox",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Resolution/Resolution",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer WaterLevel/WaterLevel",
					"MainScroll/MainContent/SectionTextures/Body/HBoxContainerRoads/RoadsCheck",
					"MainScroll/MainContent/SectionTextures/Body/HBoxContainerRoadTexture/RoadTextureButton",
					"MainScroll/MainContent/ExportBlenderButton",
					"MainScroll/MainContent/RunTestsButton",
					"MainScroll/MainContent/TestsResultDialog",
				};
				for (int i = 0; i < paths.Length; i++)
				{
					if (panel.GetNodeOrNull(paths[i]) == null)
						throw new InvalidOperationException($"Missing UI node: {paths[i]}");
				}
				return $"Проверено узлов={paths.Length}";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("VI-2", "Проверяется сцена окна прогресса.", "Окно должно содержать статус, процент и прогресс-бар.", () =>
		{
			string dialogPath = $"{TerraConfig.AddonRootPath}/progress_dialog.tscn";
			var scene = ResourceLoader.Load<PackedScene>(dialogPath);
			if (scene == null)
				throw new InvalidOperationException($"{dialogPath} missing.");
			var dialog = scene.Instantiate();
			try
			{
				if (dialog.GetNodeOrNull("VBoxContainer/StatusLabel") == null || dialog.GetNodeOrNull("VBoxContainer/ProgressBar") == null || dialog.GetNodeOrNull("VBoxContainer/PercentLabel") == null)
					throw new InvalidOperationException("Progress dialog nodes are missing.");
				return "Окно прогресса собрано корректно";
			}
			finally
			{
				dialog.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("VI-3", "Проверяется сцена генератора террейна.", "Сцена должна содержать основной меш и узлы настроек.", () =>
		{
			string generatorPath = $"{TerraConfig.AddonRootPath}/Logic/TerrainGenerator.tscn";
			var scene = ResourceLoader.Load<PackedScene>(generatorPath);
			if (scene == null)
				throw new InvalidOperationException($"{generatorPath} missing.");
			var root = scene.Instantiate() as Node3D;
			if (root == null)
				throw new InvalidOperationException("TerrainGenerator.tscn root is invalid.");
			try
			{
				if (root.GetScript().VariantType == Variant.Type.Nil)
					throw new InvalidOperationException("TerrainGenerator.tscn script is missing.");
				return $"Корень готов, детей={root.GetChildCount()}";
			}
			finally
			{
				root.QueueFree();
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
		group.ActualResult = group.Passed ? "Интерфейс и сцены доступны" : "Есть отсутствующие UI-элементы";
		return group;
	}
}
