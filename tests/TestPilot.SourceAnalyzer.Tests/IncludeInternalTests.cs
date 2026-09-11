using TestPilot.SourceAnalyzer;

namespace TestPilot.SourceAnalyzer.Tests;

// Fixture propia en disco: una solución mínima con una clase pública y una internal,
// para comprobar el flag includeInternal sin depender de la solución real de TestPilot.
public class IncludeInternalTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _slnxPath;

    public IncludeInternalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"testpilot-analyzer-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        File.WriteAllText(Path.Combine(_tempDir, "FixtureProject.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(_tempDir, "Classes.cs"), """
            namespace FixtureProject;

            public class PublicSample
            {
                public void PublicMethod() { }
            }

            internal class InternalSample
            {
                public void HandlerMethod() { }
            }
            """);

        _slnxPath = Path.Combine(_tempDir, "Fixture.slnx");
        File.WriteAllText(_slnxPath, """
            <Solution>
              <Project Path="FixtureProject.csproj" />
            </Solution>
            """);
    }

    [Fact]
    public async Task AnalyzeAsync_ByDefault_SkipsInternalClasses()
    {
        var analyzer = new SourceAnalyzerService();

        var classes = await analyzer.AnalyzeAsync(_slnxPath);

        Assert.Contains(classes, c => c.FullName.EndsWith("PublicSample"));
        Assert.DoesNotContain(classes, c => c.FullName.EndsWith("InternalSample"));
    }

    [Fact]
    public async Task AnalyzeAsync_WithIncludeInternal_ReturnsInternalClassesToo()
    {
        var analyzer = new SourceAnalyzerService();

        var classes = await analyzer.AnalyzeAsync(_slnxPath, includeInternal: true);

        Assert.Contains(classes, c => c.FullName.EndsWith("PublicSample"));
        var internalClass = Assert.Single(classes, c => c.FullName.EndsWith("InternalSample"));
        Assert.Contains(internalClass.PublicMethods, m => m.Name == "HandlerMethod");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }
}
