using TestPilot.SourceAnalyzer;

namespace TestPilot.SourceAnalyzer.Tests;

// Las clases estáticas quedaban fuera dos veces: `static class` se compila como abstract sealed
// (la descartaba el filtro de abstractas) y sus métodos son estáticos (los descartaba !IsStatic).
public class StaticMemberTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _slnxPath;

    public StaticMemberTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"testpilot-static-{Guid.NewGuid():N}");
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

            public static class StringHelper
            {
                public static string Reverse(string input) => new(input.Reverse().ToArray());
                public static bool IsPalindrome(string input) => input == Reverse(input);
            }

            public static class StringExtensions
            {
                public static int WordCount(this string input) => input.Split(' ').Length;
            }

            public class Mixed
            {
                public int Instance(int a) => a;
                public static Mixed Create() => new();
            }

            public abstract class ReallyAbstract
            {
                public abstract void Nope();
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
    public async Task AnalyzeAsync_ConClaseEstatica_LaDetectaConSusMetodos()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var helper = Assert.Single(classes, c => c.FullName == "FixtureProject.StringHelper");
        var names = helper.PublicMethods.Select(m => m.Name).OrderBy(n => n).ToList();

        Assert.Equal(["IsPalindrome", "Reverse"], names);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_MetodoEstatico_LoMarcaEnLaFirma()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var helper = Assert.Single(classes, c => c.FullName == "FixtureProject.StringHelper");
        var reverse = Assert.Single(helper.PublicMethods, m => m.Name == "Reverse");

        Assert.StartsWith("static ", reverse.Signature);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_MetodoDeExtension_LoDetecta()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var extensions = Assert.Single(classes, c => c.FullName == "FixtureProject.StringExtensions");
        Assert.Contains(extensions.PublicMethods, m => m.Name == "WordCount");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_ClaseMixta_DevuelveInstanciaYEstaticoYSoloMarcaElEstatico()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var mixed = Assert.Single(classes, c => c.FullName == "FixtureProject.Mixed");

        var instance = Assert.Single(mixed.PublicMethods, m => m.Name == "Instance");
        Assert.DoesNotContain("static ", instance.Signature);

        var create = Assert.Single(mixed.PublicMethods, m => m.Name == "Create");
        Assert.StartsWith("static ", create.Signature);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_ClaseAbstractaDeVerdad_SigueExcluida()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        Assert.DoesNotContain(classes, c => c.FullName == "FixtureProject.ReallyAbstract");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }
}
