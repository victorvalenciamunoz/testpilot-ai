namespace TestPilot.Core.Models;

public record ReviewFinding(
    ReviewSeverity Severity,
    string File,
    int? Line,
    string Category,
    string Description,
    string? Suggestion);
