namespace TestPilot.Core.Models;

public record TestRunResult(bool Success, int Passed, int Failed, IReadOnlyList<string> Errors);
