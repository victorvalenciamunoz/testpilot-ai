using Microsoft.Extensions.Logging.Abstractions;
using TestPilot.TestRunner;

namespace TestPilot.TestRunner.Tests;

// Integración: el proyecto temporal tiene dos clases de test, una sana y otra rota a propósito.
// Sin filtro `dotnet test` falla por la rota; con filtro debe aislar la sana.
[Trait("Category", "Integration")]
public class TestClassFilterTests : IDisposable
{
    private readonly string _tempProjectDir;

    public TestClassFilterTests()
    {
        _tempProjectDir = Path.Combine(Path.GetTempPath(), $"testpilot-filter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempProjectDir);

        File.WriteAllText(Path.Combine(_tempProjectDir, "SampleProject.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
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

        // Simula el test recién generado: sano.
        File.WriteAllText(Path.Combine(_tempProjectDir, "CalculatorTests.cs"), """
            namespace SampleProject.Tests;

            public class CalculatorTests
            {
                [Fact]
                public void Add_DosNumeros_DevuelveLaSuma() => Assert.Equal(2, 1 + 1);
            }
            """);

        // Simula un test preexistente roto, ajeno al que se acaba de generar.
        File.WriteAllText(Path.Combine(_tempProjectDir, "LegacyTests.cs"), """
            namespace SampleProject.Tests;

            public class LegacyTests
            {
                [Fact]
                public void Test_Roto_De_Antes() => Assert.Equal(3, 1 + 1);
            }
            """);
    }

    [Fact]
    public async Task RunAsync_SinFiltro_FallaPorElTestAjenoRoto()
    {
        var runner = new TestRunnerService(NullLogger<TestRunnerService>.Instance);

        var result = await runner.RunAsync(_tempProjectDir);

        Assert.False(result.Success);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task RunAsync_ConFiltroDeLaClaseGenerada_AislaYPasa()
    {
        var runner = new TestRunnerService(NullLogger<TestRunnerService>.Instance);

        var result = await runner.RunAsync(_tempProjectDir, "CalculatorTests");

        Assert.True(result.Success, $"Errores: {string.Join(" | ", result.Errors)}");
        Assert.Equal(1, result.Passed);
        Assert.Equal(0, result.Failed);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempProjectDir, recursive: true); }
        catch { }
    }
}
