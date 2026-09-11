using TestPilot.SourceAnalyzer;

namespace TestPilot.SourceAnalyzer.Tests;

public class SourceAnalyzerIntegrationTests
{
    private static string FindSolutionPath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(SourceAnalyzerIntegrationTests).Assembly.Location)!;
        // bin/Debug/net10.0 → up 5 levels to repo root → src/TestPilot.slnx
        return Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "..", "src", "TestPilot.slnx"));
    }

    [Fact]
    public async Task AnalyzeAsync_WithTestPilotSolution_ReturnsPublicClasses()
    {
        var solutionPath = FindSolutionPath();
        Assert.True(File.Exists(solutionPath), $"Solution not found at: {solutionPath}");

        var analyzer = new SourceAnalyzerService();
        var classes = await analyzer.AnalyzeAsync(solutionPath);

        Assert.NotEmpty(classes);
        Assert.All(classes, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.FullName));
            Assert.False(string.IsNullOrEmpty(c.FilePath));
        });
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotReturnTestProjectClasses()
    {
        var solutionPath = FindSolutionPath();
        Assert.True(File.Exists(solutionPath), $"Solution not found at: {solutionPath}");

        var analyzer = new SourceAnalyzerService();
        var classes = await analyzer.AnalyzeAsync(solutionPath);

        Assert.DoesNotContain(classes, c =>
            c.FullName.Contains(".Tests.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_PublicMethodsHaveSignatures()
    {
        var solutionPath = FindSolutionPath();
        Assert.True(File.Exists(solutionPath), $"Solution not found at: {solutionPath}");

        var analyzer = new SourceAnalyzerService();
        var classes = await analyzer.AnalyzeAsync(solutionPath);

        var classesWithMethods = classes.Where(c => c.PublicMethods.Count > 0).ToList();
        Assert.All(classesWithMethods, c =>
            Assert.All(c.PublicMethods, m =>
            {
                Assert.False(string.IsNullOrEmpty(m.Name));
                Assert.False(string.IsNullOrEmpty(m.Signature));
            }));
    }
}
