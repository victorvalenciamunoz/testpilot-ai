using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;

namespace TestPilot.ReportGenerator;

public sealed class ReportGeneratorService : IReportGenerator
{
    public AnalysisReport Generate(IReadOnlyList<ClassInfo> classes, IReadOnlyList<GeneratedTest> tests)
    {
        var classesWithTests = classes.Count(c => c.PublicMethods.Any(m => m.HasTests));
        return new AnalysisReport(classes.Count, classesWithTests, tests);
    }
}
