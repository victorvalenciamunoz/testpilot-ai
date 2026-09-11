using Microsoft.Extensions.Logging.Abstractions;
using TestPilot.SourceAnalyzer;
using TestPilot.TestRunner;

namespace TestPilot.SourceAnalyzer.Tests;

// El caso que motiva usar cobertura real: la heurística por nombre marca `Order` como testeada
// solo porque existe `OrderServiceTests`, que "contiene" la subcadena "Order". Nadie ha testeado
// `Order` nunca.
[Trait("Category", "Integration")]
public class CoverageVsHeuristicTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _slnxPath;
    private readonly string _testProjectDir;

    public CoverageVsHeuristicTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"testpilot-cov-{Guid.NewGuid():N}");
        var srcDir = Path.Combine(_tempDir, "src");
        _testProjectDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(_testProjectDir);

        File.WriteAllText(Path.Combine(srcDir, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(srcDir, "Types.cs"), """
            namespace Sample;

            public class Order
            {
                public decimal Total(decimal unit, int quantity) => unit * quantity;
            }

            public class OrderService
            {
                public string Describe(string reference) => $"Pedido {reference}";
            }
            """);

        File.WriteAllText(Path.Combine(_testProjectDir, "Sample.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="coverlet.collector" Version="6.0.4" />
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
              </ItemGroup>
              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="..\src\Sample.csproj" />
              </ItemGroup>
            </Project>
            """);

        // Solo se testea OrderService. Order no se toca.
        File.WriteAllText(Path.Combine(_testProjectDir, "OrderServiceTests.cs"), """
            using Sample;

            namespace Sample.Tests;

            public class OrderServiceTests
            {
                [Fact]
                public void Describe_ConReferencia_DevuelveElTexto()
                {
                    var result = new OrderService().Describe("A-1");
                    Assert.Equal("Pedido A-1", result);
                }
            }
            """);

        _slnxPath = Path.Combine(_tempDir, "Fixture.slnx");
        File.WriteAllText(_slnxPath, """
            <Solution>
              <Project Path="src/Sample.csproj" />
              <Project Path="tests/Sample.Tests.csproj" />
            </Solution>
            """);
    }

    [Fact]
    public async Task AnalyzeAsync_SinCobertura_MarcaOrderComoTesteadaPorFalsoPositivo()
    {
        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath);

        var order = Assert.Single(classes, c => c.FullName == "Sample.Order");

        // "OrderServiceTests".Contains("Order") es cierto: la heurística se equivoca.
        Assert.All(order.PublicMethods, m => Assert.True(m.HasTests));
    }

    [Fact]
    public async Task AnalyzeAsync_ConCobertura_DetectaQueOrderNoEstaTesteada()
    {
        var (coverage, error) = await new CoverageCollector(NullLogger<CoverageCollector>.Instance)
            .CollectAsync(_testProjectDir);
        Assert.Null(error);

        var classes = await new SourceAnalyzerService().AnalyzeAsync(_slnxPath, coverage: coverage);

        var order = Assert.Single(classes, c => c.FullName == "Sample.Order");
        Assert.All(order.PublicMethods, m => Assert.False(m.HasTests, $"{m.Name} no debería figurar como cubierto"));

        var service = Assert.Single(classes, c => c.FullName == "Sample.OrderService");
        Assert.Contains(service.PublicMethods, m => m.Name == "Describe" && m.HasTests);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }
}
