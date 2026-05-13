public sealed class TestOperationResult
{
	public TestOperationResult(string name, string whatIsChecked, string expectedResult, string actualResult, bool passed, long durationMs, string errorMessage = "")
	{
		Name = name;
		WhatIsChecked = whatIsChecked;
		ExpectedResult = expectedResult;
		ActualResult = actualResult;
		Passed = passed;
		DurationMs = durationMs;
		ErrorMessage = errorMessage;
	}

	public string Name { get; }
	public string WhatIsChecked { get; }
	public string ExpectedResult { get; }
	public string ActualResult { get; }
	public bool Passed { get; }
	public long DurationMs { get; }
	public string ErrorMessage { get; }
}
