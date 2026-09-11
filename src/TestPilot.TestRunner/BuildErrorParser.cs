using System.Text.RegularExpressions;

namespace TestPilot.TestRunner;

public static partial class BuildErrorParser
{
    public static IReadOnlyList<string> ParseCompilerErrors(string output)
    {
        return CompilerDiagnosticRegex()
            .Matches(output)
            .Where(m => m.Groups[4].Value == "error")
            .Select(m => $"{m.Groups[1].Value}({m.Groups[2].Value},{m.Groups[3].Value}): {m.Groups[5].Value}: {m.Groups[6].Value}")
            .ToList();
    }

    [GeneratedRegex(@"^(.+)\((\d+),(\d+)\):\s+(error|warning)\s+(CS\d+):\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex CompilerDiagnosticRegex();
}
