namespace TestPilot.Core.Text;

public static class MarkdownFenceStripper
{
    // Los LLMs a veces envuelven su respuesta en ```json / ```csharp ... ``` aunque se les pida que no.
    public static string Strip(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0)
            trimmed = trimmed[(firstNewline + 1)..];
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3].TrimEnd();

        return trimmed.Trim();
    }
}
