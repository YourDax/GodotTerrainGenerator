using Godot;
using System;
using System.Text;

public static class DetailedTestReportWriter
{
	public static void Write(string reportPath, TestRunOutcome outcome, DateTime startedAt, DateTime finishedAt)
	{
		var builder = new StringBuilder();
		builder.AppendLine("Отчет о тестировании проекта TerraGenerating");
		builder.AppendLine($"Начало: {startedAt:yyyy-MM-dd HH:mm:ss}");
		builder.AppendLine($"Окончание: {finishedAt:yyyy-MM-dd HH:mm:ss}");
		builder.AppendLine($"Общее время выполнения: {outcome.TotalDurationMs} мс");
		builder.AppendLine($"Итог: {(outcome.Passed ? "все тесты пройдены" : "есть ошибки")}");
		builder.AppendLine();

		int passedGroups = 0;
		int passedOperations = 0;
		int totalOperations = 0;

		foreach (var group in outcome.Groups)
		{
			if (group.Passed)
				passedGroups++;

			builder.AppendLine($"Блок: {group.Name}");
			builder.AppendLine($"Что проверяется: {group.WhatIsChecked}");
			builder.AppendLine($"Ожидаемый результат блока: {group.ExpectedResult}");
			builder.AppendLine($"Получившийся результат блока: {group.ActualResult}");
			builder.AppendLine($"Статус блока: {(group.Passed ? "пройден" : "не пройден")}");
			builder.AppendLine($"Время блока: {group.DurationMs} мс");

			foreach (var operation in group.Operations)
			{
				totalOperations++;
				if (operation.Passed)
					passedOperations++;

				builder.AppendLine($"  Операция: {operation.Name}");
				builder.AppendLine($"  Что проверяется: {operation.WhatIsChecked}");
				builder.AppendLine($"  Ожидаемый результат: {operation.ExpectedResult}");
				builder.AppendLine($"  Получившийся результат: {operation.ActualResult}");
				builder.AppendLine($"  Статус: {(operation.Passed ? "пройден" : "не пройден")}");
				builder.AppendLine($"  Время операции: {operation.DurationMs} мс");
				if (!string.IsNullOrWhiteSpace(operation.ErrorMessage))
					builder.AppendLine($"  Ошибка: {operation.ErrorMessage}");
			}

			builder.AppendLine();
		}

		builder.AppendLine($"Пройдено блоков: {passedGroups}/{outcome.Groups.Count}");
		builder.AppendLine($"Пройдено операций: {passedOperations}/{totalOperations}");

		try
		{
			using var file = FileAccess.Open(reportPath, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PrintErr($"[Tests] Could not open report file for writing: {reportPath}");
				return;
			}
			file.StoreString(builder.ToString());
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Tests] Failed to write detailed report: {ex.Message}");
		}
	}
}
