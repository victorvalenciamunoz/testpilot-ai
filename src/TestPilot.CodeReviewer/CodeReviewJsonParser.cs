using System.Text.Json;
using System.Text.Json.Serialization;
using TestPilot.Core.Models;
using TestPilot.Core.Text;

namespace TestPilot.CodeReviewer;

public static class CodeReviewJsonParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static CodeReviewResult Parse(string llmResponse)
    {
        var json = MarkdownFenceStripper.Strip(llmResponse);
        return JsonSerializer.Deserialize<CodeReviewResult>(json, _options)
            ?? throw new InvalidOperationException("La respuesta del LLM se deserializó como null.");
    }
}
