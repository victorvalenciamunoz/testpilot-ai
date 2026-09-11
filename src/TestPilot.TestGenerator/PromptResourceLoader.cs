using System.Reflection;

namespace TestPilot.TestGenerator;

internal static class PromptResourceLoader
{
    public static string LoadEmbeddedPrompt(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
