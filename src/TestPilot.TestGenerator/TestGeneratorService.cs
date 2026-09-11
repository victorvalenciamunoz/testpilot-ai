using Microsoft.Extensions.AI;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;
using TestPilot.Core.Text;

namespace TestPilot.TestGenerator;

public sealed class TestGeneratorService : ITestGenerator
{
    // Temperatura baja para generar código: favorece el token más probable, que es el más
    // idiomático y el que menos APIs inventadas produce. No da reproducibilidad — medido contra
    // Groq, dos generaciones de la misma clase a 0 difieren igualmente.
    private static readonly ChatOptions _chatOptions = new() { Temperature = 0 };

    private readonly IChatClient _chatClient;

    // Prompt cargado una sola vez desde el recurso embebido al arrancar.
    private static readonly string _promptTemplate =
        PromptResourceLoader.LoadEmbeddedPrompt("TestPilot.TestGenerator.Prompts.GenerateTests.txt");

    public TestGeneratorService(IChatClient chatClient) => _chatClient = chatClient;

    public async Task<GeneratedTest> GenerateAsync(ClassInfo classInfo, string? mockingLibrary = null)
    {
        var sourceCode = File.Exists(classInfo.FilePath)
            ? await File.ReadAllTextAsync(classInfo.FilePath)
            : string.Empty;

        var methods = string.Join("\n", classInfo.PublicMethods.Select(m => $"  - {m.Signature}"));

        var prompt = _promptTemplate
            .Replace("{{$className}}", classInfo.FullName)
            .Replace("{{$methods}}", methods)
            .Replace("{{$sourceCode}}", sourceCode)
            .Replace("{{$mockingPolicy}}", MockingPolicy.Describe(mockingLibrary));

        var response = await _chatClient.GetResponseAsync(prompt, _chatOptions);
        var testCode = MarkdownFenceStripper.Strip(response.Text);

        return new GeneratedTest(
            ClassName: $"{classInfo.FullName}Tests",
            TestCode: testCode,
            TargetClass: classInfo.FullName);
    }
}
