using Microsoft.Extensions.Logging.Abstractions;
using TestPilot.TestRunner;

namespace TestPilot.TestRunner.Tests;

// Un informe vacío haría que todas las clases pareciesen sin cubrir y se generasen tests para la
// solución entera, así que estos casos tienen que devolver error y no un CoverageReport vacío.
public class CoverageCollectorTests : IDisposable
{
    private readonly string _tempDir;

    public CoverageCollectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"testpilot-covcol-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task CollectAsync_ProyectoInexistente_DevuelveErrorYNoInforme()
    {
        var collector = new CoverageCollector(NullLogger<CoverageCollector>.Instance);

        var (report, error) = await collector.CollectAsync(Path.Combine(_tempDir, "no-existe"));

        Assert.Null(report);
        Assert.Contains("No se encuentra el proyecto de tests", error);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CollectAsync_ProyectoSinCoverletCollector_DevuelveErrorQueLoExplica()
    {
        File.WriteAllText(Path.Combine(_tempDir, "SinCoverlet.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
              </ItemGroup>
              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "Tests.cs"), """
            namespace SinCoverlet;

            public class Tests
            {
                [Fact]
                public void Pasa() => Assert.True(true);
            }
            """);

        var collector = new CoverageCollector(NullLogger<CoverageCollector>.Instance);

        var (report, error) = await collector.CollectAsync(_tempDir);

        Assert.Null(report);
        Assert.Contains("coverlet.collector", error);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }
}
