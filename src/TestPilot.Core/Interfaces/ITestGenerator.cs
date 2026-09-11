using TestPilot.Core.Models;

namespace TestPilot.Core.Interfaces;

public interface ITestGenerator
{
    Task<GeneratedTest> GenerateAsync(ClassInfo classInfo, string? mockingLibrary = null);
}
