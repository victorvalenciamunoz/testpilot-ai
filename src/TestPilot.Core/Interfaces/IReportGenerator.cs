using TestPilot.Core.Models;

namespace TestPilot.Core.Interfaces;

public interface IReportGenerator
{
    AnalysisReport Generate(IReadOnlyList<ClassInfo> classes, IReadOnlyList<GeneratedTest> tests);
}
