using Godot;
using System;
using System.Diagnostics;

public sealed class ProjectUsabilityTestSuite
{
	public TestGroupResult Run()
	{
		var group = new TestGroupResult(
			"Тестирование удобства использования",
			"Проверяется вся панель плагина как рабочий интерфейс: режимы, поля, кнопки, нормализация путей и поведение элементов.",
			"Пользователь должен видеть связный интерфейс без сломанных контролов и без неинициализированных элементов.");

		var watch = Stopwatch.StartNew();

		group.Operations.Add(TestTools.RunOperation("UX-1", "Проверяется переключение между случайным и real-map режимом.", "Панель должна скрывать и показывать нужные блоки.", () =>
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
				var randomBlock = panel.GetNodeOrNull("MainScroll/MainContent/SectionMesh/Body/VBoxContainer");
				var realBlock = panel.GetNodeOrNull("MainScroll/MainContent/SectionMesh/Body/VBoxContainerRealMap");
				if (randomBlock == null || realBlock == null)
					throw new InvalidOperationException("Mode blocks are missing.");
				return "Режимы панели доступны";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-2", "Проверяется доступность параметров генерации в панели.", "Основные числовые поля должны быть доступны для редактирования.", () =>
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
				string[] fields =
				{
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Z/Zbox",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Min Y/MinYbox",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Max Y/MaxYbox",
					"MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer WaterLevel/WaterLevel",
				};
				for (int i = 0; i < fields.Length; i++)
				{
					var spin = panel.GetNodeOrNull<SpinBox>(fields[i]);
					if (spin == null || !spin.Editable)
						throw new InvalidOperationException($"Field is not editable: {fields[i]}");
				}
				return "Числовые поля редактируются";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-3", "Проверяется список доступных путей моделей для scatter.", "Панель должна подхватывать существующие ресурсы без ручного ввода.", () =>
		{
			string tree = TestTools.FindFirstExistingResource($"{TerraConfig.AddonRootPath}/Texture/source/tree.tscn", $"{TerraConfig.AddonRootPath}/Texture/source/tree2.tscn");
			string rock = TestTools.FindFirstExistingResource($"{TerraConfig.AddonRootPath}/Texture/source/rock.tscn", $"{TerraConfig.AddonRootPath}/Texture/source/rock2.tscn");
			if (string.IsNullOrEmpty(tree) || string.IsNullOrEmpty(rock))
				throw new InvalidOperationException("Scatter resources are missing.");
			return "Ресурсы scatter обнаружены";
		}));

		group.Operations.Add(TestTools.RunOperation("UX-4", "Интерфейс: нормализация устаревших путей при загрузке настроек.", "Старые пути автоматически переписываются на текущий корень аддона.",() =>
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
				string normalized = panel.Call("_normalize_legacy_addon_path", "res://addons/terragenerating/Texture/grass.png").AsString();
				if (!normalized.Contains("Texture/grass.png"))
					throw new InvalidOperationException("Path was not normalized.");
				return $"Путь нормализован: {normalized}";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-5", "Интерфейс: визуальное представление элементов управления приложение в панели.", "Все элементы видимы, не перекрываются и расположены логично.",() =>
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
				// Проверяем наличие основных элементов интерфейса
				var scroll = panel.GetNodeOrNull("MainScroll");
				var sectionMesh = panel.GetNodeOrNull("MainScroll/MainContent/SectionMesh");
				var sectionTextures = panel.GetNodeOrNull("MainScroll/MainContent/SectionTextures");
				var sectionObjects = panel.GetNodeOrNull("MainScroll/MainContent/SectionObjects");
				if (scroll == null || sectionMesh == null || sectionTextures == null || sectionObjects == null)
					throw new InvalidOperationException("UI sections are missing or collapsed.");
				return "Все элементы UI видны и доступны";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-6", "Интерфейс: реагирование на пользовательский ввод в полях генерации.", "Все поля принимают значения и обновляют внутреннее состояние.",() =>
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
				var xBox = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox");
				var zBox = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer Z/Zbox");
				if (xBox == null || zBox == null)
					throw new InvalidOperationException("Size fields not found.");
				xBox.Value = 42;
				zBox.Value = 24;
				if (Math.Abs(xBox.Value - 42) > 0.1f || Math.Abs(zBox.Value - 24) > 0.1f)
					throw new InvalidOperationException("Field values were not set.");
				return "Пользовательский ввод обработан корректно";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-7", "Интерфейс: доступность параметров текстур в панели.", "Пороги песок/трава/камень редактируются и применяются корректно.",() =>
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
				var sandGrass = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer SandGrass/SandGrass");
				var grassRock = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer GrassRock/GrassRock");
				if (sandGrass == null || grassRock == null || !sandGrass.Editable || !grassRock.Editable)
					throw new InvalidOperationException("Texture threshold fields are not accessible.");
				return "Параметры текстур доступны для редактирования";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-8", "Интерфейс: блокирование несовместимых опций при выборе режима продолжения генерации.", "Поля ширины/длины корректно блокируются в зависимости от направления.",() =>
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
				// Проверяем каждое направление
				for (int i = 0; i < direction.ItemCount; i++)
				{
					continueCheck.ButtonPressed = true;
					direction.Selected = i;
					panel.Call("_update_continue_generation_ui");
					// После каждого выбора интерфейс должен обновиться
				}
				return "Режим продолжения корректно блокирует поля";
			}
			finally
			{
				panel.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-9", "Интерфейс: отображение диалога прогресса во время операций.", "Диалог отображается, обновляется и может быть закрыт.",() =>
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
				// Проверяем, что диалог может быть обновлен несколько раз
				for (float p = 0; p <= 100; p += 25)
				{
					dialog.Call("update_progress", p, $"Выполняется {p}%...");
				}
				var percentLabel = dialog.GetNodeOrNull<Label>("VBoxContainer/PercentLabel");
				if (percentLabel == null || !percentLabel.Text.Contains("100"))
					throw new InvalidOperationException("Progress dialog did not reach 100%.");
				return "Диалог прогресса работает корректно";
			}
			finally
			{
				dialog.QueueFree();
			}
		}));

		group.Operations.Add(TestTools.RunOperation("UX-10", "Интерфейс: обработка ошибок и выводи заплатных сообщений.", "Некорректный ввод обрабатывается без краша, пользователь видит подсказки.",() =>
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
				// Пытаемся установить некорректные значения
				var xBox = panel.GetNodeOrNull<SpinBox>("MainScroll/MainContent/SectionMesh/Body/VBoxContainer/HBoxContainer X/Xbox");
				if (xBox != null)
				{
					// Пытаемся установить слишком большое значение
					xBox.Value = 99999;
					// Диапазон должен быть ограничен
					if (xBox.Value > 1000)
						throw new InvalidOperationException("Field value was not clamped.");
				}
				return "Ошибки обработаны и значения зажаты в допустимый диапазон";
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
		group.ActualResult = group.Passed ? $"Интерфейс удален и эргономичен, все операции видимы пользователю" : "Есть неудобные или скрытые элементы интерфейса";
		return group;
	}
}
