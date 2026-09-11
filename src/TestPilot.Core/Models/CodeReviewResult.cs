namespace TestPilot.Core.Models;

public record CodeReviewResult(string Summary, IReadOnlyList<ReviewFinding> Findings);
