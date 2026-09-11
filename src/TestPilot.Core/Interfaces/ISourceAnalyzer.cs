using TestPilot.Core.Models;

namespace TestPilot.Core.Interfaces;

public interface ISourceAnalyzer
{
    Task<IReadOnlyList<ClassInfo>> AnalyzeAsync(string solutionPath, bool includeInternal = false, CoverageReport? coverage = null);
}
