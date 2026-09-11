namespace TestPilot.TestGenerator;

public static class TestOutputPathValidator
{
    // El bucle corrector ejecuta `dotnet test` sobre testProjectPath. Si el test generado se
    // escribe fuera de esa carpeta, el proyecto nunca lo compila: el bucle se ejecuta, no ve el
    // fichero nuevo y da por bueno (o por malo) algo que no está evaluando. Se avisa antes en vez
    // de dejar que falle en silencio.
    public static string? Validate(string outputPath, string testProjectPath)
    {
        var projectDir = File.Exists(testProjectPath)
            ? Path.GetDirectoryName(Path.GetFullPath(testProjectPath))
            : testProjectPath;

        if (string.IsNullOrWhiteSpace(projectDir))
            return $"No se puede determinar la carpeta del proyecto de tests a partir de: {testProjectPath}";

        var projectFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectDir));
        var outputFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath));

        var isInside = outputFull.Equals(projectFull, StringComparison.OrdinalIgnoreCase)
            || outputFull.StartsWith(projectFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        return isInside
            ? null
            : $"'outputPath' ({outputFull}) está fuera del proyecto de tests ({projectFull}): el test generado no formaría parte de ese proyecto y el bucle corrector nunca lo compilaría. Indica un outputPath dentro del proyecto, o omite testProjectPath para generar sin ejecutar.";
    }
}
