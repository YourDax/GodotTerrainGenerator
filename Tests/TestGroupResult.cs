using System.Collections.Generic;

public sealed class TestGroupResult
{
	public TestGroupResult(string name, string whatIsChecked, string expectedResult)
	{
		Name = name;
		WhatIsChecked = whatIsChecked;
		ExpectedResult = expectedResult;
		Operations = new List<TestOperationResult>();
	}

	public string Name { get; }
	public string WhatIsChecked { get; }
	public string ExpectedResult { get; }
	public string ActualResult { get; set; } = string.Empty;
	public bool Passed { get; set; }
	public long DurationMs { get; set; }
	public List<TestOperationResult> Operations { get; }
}
