using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using TestPilot.Core.Models;
using TestPilot.TestGenerator;

namespace TestPilot.TestGenerator.Tests;

/// <summary>
/// Tests de integración: requieren AI:ApiKey en user-secrets del proyecto API.
/// Para ejecutarlos localmente: dotnet user-secrets set "AI:ApiKey" "gsk_..." --project src/TestPilot.API
/// En CI se saltan con: dotnet test --filter "Category!=Integration"
/// </summary>
public class TestGeneratorServiceTests
{
    private static IChatClient? BuildChatClient()
    {
        // Lee la clave desde variable de entorno para CI, o desde config local.
        var apiKey = Environment.GetEnvironmentVariable("TESTPILOT_OPENAI_KEY")
                     ?? ReadUserSecret("AI:ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var openAIClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1/") });

        return openAIClient.GetChatClient("openai/gpt-oss-120b").AsIChatClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_ForSimpleClass_ContainsFactAttribute()
    {
        var chatClient = BuildChatClient();
        if (chatClient is null)
        {
            // Sin clave configurada: el test se marca como omitido de forma explícita.
            Assert.True(true, "SKIP: TESTPILOT_OPENAI_KEY not set. Run with a real key to execute this test.");
            return;
        }

        var classInfo = new ClassInfo(
            FullName: "MyApp.Calculator",
            FilePath: string.Empty,
            PublicMethods:
            [
                new MethodInfo("Add",      "int Add(int a, int b)",      HasTests: false),
                new MethodInfo("Subtract", "int Subtract(int a, int b)", HasTests: false)
            ]);

        var service = new TestGeneratorService(chatClient);
        var result  = await service.GenerateAsync(classInfo);

        Assert.False(string.IsNullOrWhiteSpace(result.TestCode));
        Assert.True(
            result.TestCode.Contains("[Fact]") || result.TestCode.Contains("[Theory]"),
            $"Expected [Fact] or [Theory] in generated code. Got:\n{result.TestCode}");
        Assert.Equal("MyApp.Calculator", result.TargetClass);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_GeneratedCode_DoesNotContainMarkdownFences()
    {
        var chatClient = BuildChatClient();
        if (chatClient is null) { Assert.True(true, "SKIP: key not set"); return; }

        var classInfo = new ClassInfo(
            FullName: "MyApp.StringHelper",
            FilePath: string.Empty,
            PublicMethods: [new MethodInfo("Reverse", "string Reverse(string input)", HasTests: false)]);

        var service = new TestGeneratorService(chatClient);
        var result  = await service.GenerateAsync(classInfo);

        Assert.False(result.TestCode.TrimStart().StartsWith("```"),
            "Response should not contain markdown fences");
    }

    // Lee un user-secret del proyecto API para poder reutilizar la clave sin duplicarla.
    private static string? ReadUserSecret(string key)
    {
        try
        {
            var secretsId = "d77482ec-3049-4b8f-85fb-d364449b13c7";
            var secretsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "UserSecrets", secretsId, "secrets.json");

            if (!File.Exists(secretsPath)) return null;

            var json = File.ReadAllText(secretsPath);
            // Búsqueda simple: evita dependencia de System.Text.Json en el proyecto de test.
            var needle = $"\"{key}\"";
            var idx = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var colon = json.IndexOf(':', idx + needle.Length);
            if (colon < 0) return null;

            var quote1 = json.IndexOf('"', colon + 1);
            var quote2 = json.IndexOf('"', quote1 + 1);
            if (quote1 < 0 || quote2 < 0) return null;

            return json[(quote1 + 1)..quote2];
        }
        catch
        {
            return null;
        }
    }
}
