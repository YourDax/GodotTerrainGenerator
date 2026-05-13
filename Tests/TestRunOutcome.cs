using System;
using System.Collections.Generic;

public sealed class TestRunOutcome
{
	// Сводит результаты всех тестовых групп в один прогон.
	public TestRunOutcome(List<TestGroupResult> groups, DateTime startedAt, DateTime finishedAt, long totalDurationMs, bool passed)
	{
		Groups = groups;
		StartedAt = startedAt;
		FinishedAt = finishedAt;
		TotalDurationMs = totalDurationMs;
		Passed = passed;
	}

	public List<TestGroupResult> Groups { get; }
	public DateTime StartedAt { get; }
	public DateTime FinishedAt { get; }
	public long TotalDurationMs { get; }
	public bool Passed { get; }
}
