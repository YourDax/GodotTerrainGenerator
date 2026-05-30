using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public sealed class ProjectTestRunner
{
	public TestRunOutcome RunAll(Action<float, string> progress = null)
	{
		var startedAt = DateTime.Now;
		var watch = Stopwatch.StartNew();
		// Запускаем только активные наборы тестов, которые реально входят в текущий прогон.
		var steps = new (string Name, Func<TestGroupResult> Run)[]
		{
			("Модульные тесты (Godot)", () => new ProjectModuleTestSuite().Run()),
			("Функциональные тесты (Godot)", () => new ProjectFunctionalTestSuite().Run()),
			("Тестирование производительности (Godot)", () => new ProjectPerformanceTestSuite().Run()),
		};
		var groups = new List<TestGroupResult>(steps.Length);
		for (int i = 0; i < steps.Length; i++)
		{
			float start = (i / (float)steps.Length) * 100f;
			progress?.Invoke(start, $"Запуск: {steps[i].Name}");
			groups.Add(steps[i].Run());
			float done = ((i + 1) / (float)steps.Length) * 100f;
			progress?.Invoke(done, $"Готово: {steps[i].Name}");
		}
		watch.Stop();

		bool passed = true;
		for (int i = 0; i < groups.Count; i++)
		{
			if (!groups[i].Passed)
			{
				passed = false;
				break;
			}
		}

		return new TestRunOutcome(groups, startedAt, DateTime.Now, watch.ElapsedMilliseconds, passed);
	}
}
