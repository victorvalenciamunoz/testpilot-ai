using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using System.ClientModel;
using System.Text.Json;
using TestPilot.CodeReviewer;
using TestPilot.ReportGenerator;
using TestPilot.SourceAnalyzer;
using TestPilot.TestGenerator;
using TestPilot.TestRunner;

namespace TestPilot.McpServer.Tests;

// Las herramientas son una fachada sobre los servicios reales: se construyen tal cual los
// registra el Program.cs del servidor MCP. El IChatClient lleva una clave falsa a propósito;
// ninguno de estos tests llega a invocar al LLM.
public class TestPilotToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _slnxPath;
    private readonly TestPilotTools _tools;

    public TestPilotToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"testpilot-mcp-fixture-{Guid.NewGuid():N}");
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

        File.WriteAllText(Path.Combine(_tempDir, "Calculator.cs"), """
            namespace FixtureProject;

            public class Calculator
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

        var chatClient = new OpenAIClient(new ApiKeyCredential("clave-de-prueba"))
            .GetChatClient("modelo-de-prueba")
            .AsIChatClient();

        var testRunner = new TestRunnerService(NullLogger<TestRunnerService>.Instance);

        _tools = new TestPilotTools(
            new SourceAnalyzerService(),
            new TestGeneratorService(chatClient),
            testRunner,
            new CoverageCollector(NullLogger<CoverageCollector>.Instance),
            new CodeReviewerService(chatClient),
            new ReportGeneratorService(),
            new TestCorrectionAgent(chatClient, testRunner, NullLogger<TestCorrectionAgent>.Instance),
            NullLogger<TestPilotTools>.Instance);
    }

    [Fact]
    public async Task AnalyzeSolutionAsync_SolutionNotFound_ReturnsErrorJson()
    {
        var json = await _tools.AnalyzeSolutionAsync(Path.Combine(_tempDir, "NoExiste.slnx"));

        Assert.Contains("No se encuentra el archivo de solución", GetString(json, "error"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeSolutionAsync_ValidSolution_ReturnsClassesAndCounters()
    {
        var json = await _tools.AnalyzeSolutionAsync(_slnxPath);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("TotalClasses").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("ClassesWithTests").GetInt32());

        var onlyClass = Assert.Single(doc.RootElement.GetProperty("Classes").EnumerateArray());
        Assert.Equal("FixtureProject.Calculator", onlyClass.GetProperty("FullName").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GenerateTestsAsync_UnknownClass_ReturnsErrorWithoutCallingLlm()
    {
        var json = await _tools.GenerateTestsAsync(_slnxPath, "FixtureProject.NoExiste", _tempDir);

        Assert.Contains("Clase no encontrada en el análisis", GetString(json, "error"));
    }

    [Fact]
    public async Task RunTestsAsync_ProjectNotFound_ReturnsErrorJson()
    {
        var json = await _tools.RunTestsAsync(Path.Combine(_tempDir, "NoExiste.csproj"));

        Assert.Contains("No se encuentra el proyecto de tests", GetString(json, "error"));
    }

    [Fact]
    public async Task ReviewCodeAsync_EmptyDiff_ReturnsErrorWithoutCallingLlm()
    {
        var json = await _tools.ReviewCodeAsync("   ");

        Assert.Contains("El diff está vacío", GetString(json, "error"));
    }

    private static string GetString(string json, string property)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(property).GetString() ?? string.Empty;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }
}
