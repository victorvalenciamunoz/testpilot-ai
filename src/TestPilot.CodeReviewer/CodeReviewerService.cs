using System.Reflection;
using Microsoft.Extensions.AI;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;

namespace TestPilot.CodeReviewer;

public sealed class CodeReviewerService : ICodeReviewer
{
    // La revisión debe ser reproducible y devolver JSON estricto: temperatura 0 y response_format
    // json, que el proveedor impone en el propio decodificado en vez de confiar en el prompt.
    private static readonly ChatOptions _chatOptions = new()
    {
        Temperature = 0,
        ResponseFormat = ChatResponseFormat.Json
    };

    private static readonly string _promptTemplate =
        LoadEmbeddedPrompt("TestPilot.CodeReviewer.Prompts.ReviewCode.txt");

    private readonly IChatClient _chatClient;

    public CodeReviewerService(IChatClient chatClient) => _chatClient = chatClient;

    public async Task<CodeReviewResult> ReviewAsync(string diff, string? conventions = null)
    {
        // Sin convenciones el hueco de la plantilla no puede quedar vacío: dejaría el encabezado
        // de la sección colgando y el modelo se inventaría reglas para rellenarlo.
        var conventionsText = string.IsNullOrWhiteSpace(conventions)
            ? "(ninguna indicada: aplica solo las reglas genéricas anteriores)"
            : conventions;

        var prompt = _promptTemplate
            .Replace("{{$conventions}}", conventionsText)
            .Replace("{{$diff}}", diff);

        var response = await _chatClient.GetResponseAsync(prompt, _chatOptions);
        return CodeReviewJsonParser.Parse(response.Text);
    }

    private static string LoadEmbeddedPrompt(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
