using System.Text.RegularExpressions;

namespace TestPilot.TestGenerator;

public static partial class MockingLibraryDetector
{
    // Se detecta en vez de preguntar porque es un dato exacto y no una preferencia: si el paquete no
    // está referenciado, un test que lo use no compila, y el bucle corrector no puede arreglarlo
    // (no se añade un NuGet editando un .cs). La detección marca el techo de lo que es posible.
    private static readonly string[] _knownLibraries = ["Moq", "NSubstitute", "FakeItEasy"];

    public static string? Detect(string testProjectPath)
    {
        var contents = CollectProjectFiles(testProjectPath)
            .Select(File.ReadAllText)
            .ToList();

        return _knownLibraries.FirstOrDefault(
            library => contents.Any(content => ReferencesPackage(content, library)));
    }

    // Además del propio .csproj se miran los Directory.*.props de los directorios padre, donde
    // muchos repos centralizan las versiones de paquetes.
    private static IEnumerable<string> CollectProjectFiles(string testProjectPath)
    {
        var startDir = File.Exists(testProjectPath)
            ? Path.GetDirectoryName(Path.GetFullPath(testProjectPath))
            : testProjectPath;

        if (string.IsNullOrWhiteSpace(startDir) || !Directory.Exists(startDir))
            yield break;

        if (File.Exists(testProjectPath))
        {
            yield return testProjectPath;
        }
        else
        {
            foreach (var csproj in Directory.EnumerateFiles(startDir, "*.csproj"))
                yield return csproj;
        }

        var dir = new DirectoryInfo(startDir);
        for (var depth = 0; dir is not null && depth < 10; depth++, dir = dir.Parent)
        {
            foreach (var name in (string[])["Directory.Build.props", "Directory.Packages.props"])
            {
                var path = Path.Combine(dir.FullName, name);
                if (File.Exists(path))
                    yield return path;
            }
        }
    }

    // El nombre debe coincidir entero: "Moq" no puede darse por encontrado por "Moq.AutoMock".
    private static bool ReferencesPackage(string projectContent, string packageId) =>
        PackageReferenceRegex()
            .Matches(projectContent)
            .Any(m => m.Groups[1].Value.Equals(packageId, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"Package(?:Reference|Version)\s+Include\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PackageReferenceRegex();
}
