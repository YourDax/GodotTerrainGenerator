using Godot;
using System;

[Tool]
public partial class ProjectTestingSuite : RefCounted
{
	private const string ReportPath = "res://addons/terragenerating/Tests/test_report.txt";

	[Signal]
	public delegate void ProgressUpdatedEventHandler(float progress, string status);

	public bool RunAll()
	{
		var startedAt = DateTime.Now;
		GD.Print("[Tests] Starting project test suite");
		Action<float, string> progress = (value, status) =>
		{
			EmitSignal(SignalName.ProgressUpdated, value, status);
		};

		var runner = new ProjectTestRunner();
		var outcome = runner.RunAll(progress);
		DetailedTestReportWriter.Write(ReportPath, outcome, startedAt, DateTime.Now);

		if (outcome.Passed)
		{
			GD.Print("[Tests] ProjectTestingSuite: all tests passed");
			GD.Print($"[Tests] Detailed report saved: {ReportPath}");
		}
		else
		{
			GD.PrintErr("[Tests] ProjectTestingSuite: some tests failed");
		}

		return outcome.Passed;
	}
}
