using TestPilot.Core.Models;

namespace TestPilot.Core.Interfaces;

public interface ICodeReviewer
{
    Task<CodeReviewResult> ReviewAsync(string diff, string? conventions = null);
}
