using TestPilot.SourceAnalyzer;

namespace TestPilot.SourceAnalyzer.Tests;

// Los record eran invisibles para el analizador: RecordDeclarationSyntax no es un
// ClassDeclarationSyntax, son tipos hermanos bajo TypeDeclarationSyntax.
public class RecordAndStructTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _slnxPath;

    public RecordAndStructTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"testpilot-records-{Guid.NewGuid():N}");
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

        File.WriteAllText(Path.Combine(_tempDir, "Types.cs"), """
            namespace FixtureProject;

            public record Money(decimal Amount, string Currency)
            {
                public Money Add(Money other) => this with { Amount = Amount + other.Amount };
                public bool IsZero() => Amount == 0;
            }

            public record struct Point(int X, int Y)
            {
                public int ManhattanDistance() => Math.Abs(X) + Math.Abs(Y);
            }

            public struct Temperature
            {
                public double Celsius { get; init; }
                public double ToFahrenheit() => Celsius * 9 / 5 + 32;
            }

            public interface IShouldBeIgnored
            {
                void DoSomething();
            }

            public class PlainClass
            {
                public int Add(int a, int b) => a + b;
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
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_ConRecord_LoDetectaConSusMetodosDeclarados()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var money = Assert.Single(classes, c => c.FullName == "FixtureProject.Money");
        var names = money.PublicMethods.Select(m => m.Name).OrderBy(n => n).ToList();

        Assert.Equal(["Add", "IsZero"], names);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_ConRecord_NoDevuelveMiembrosSintetizadosPorElCompilador()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var money = Assert.Single(classes, c => c.FullName == "FixtureProject.Money");

        Assert.DoesNotContain(money.PublicMethods, m => m.Name is "Equals" or "GetHashCode" or "ToString" or "Deconstruct");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_ConRecordStructYStruct_LosDetecta()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var point = Assert.Single(classes, c => c.FullName == "FixtureProject.Point");
        Assert.Contains(point.PublicMethods, m => m.Name == "ManhattanDistance");

        var temperature = Assert.Single(classes, c => c.FullName == "FixtureProject.Temperature");
        Assert.Contains(temperature.PublicMethods, m => m.Name == "ToFahrenheit");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_ConInterfaz_NoLaDevuelve()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        Assert.DoesNotContain(classes, c => c.FullName == "FixtureProject.IShouldBeIgnored");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_ConClaseNormal_SigueFuncionandoComoAntes()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var plain = Assert.Single(classes, c => c.FullName == "FixtureProject.PlainClass");
        Assert.Contains(plain.PublicMethods, m => m.Name == "Add");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }
}
