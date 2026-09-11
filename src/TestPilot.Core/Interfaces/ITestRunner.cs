using TestPilot.Core.Models;

namespace TestPilot.Core.Interfaces;

public interface ITestRunner
{
    Task<TestRunResult> RunAsync(string projectPath, string? testClassFilter = null);
}
