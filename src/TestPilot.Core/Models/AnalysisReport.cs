namespace TestPilot.Core.Models;

public record AnalysisReport(int TotalClasses, int ClassesWithTests, IReadOnlyList<GeneratedTest> Generated);
