using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;

namespace TestPilot.TestRunner;

public sealed class CoverageCollector : ICoverageCollector
{
    private readonly ILogger<CoverageCollector> _logger;

    public CoverageCollector(ILogger<CoverageCollector> logger) => _logger = logger;

    public async Task<(CoverageReport? Report, string? Error)> CollectAsync(string testProjectPath)
    {
        if (!File.Exists(testProjectPath) && !Directory.Exists(testProjectPath))
            return (null, $"No se encuentra el proyecto de tests: {testProjectPath}");

        var resultsDir = Path.Combine(Path.GetTempPath(), $"testpilot-cov-{Guid.NewGuid():N}");
        Directory.CreateDirectory(resultsDir);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{testProjectPath}\" --nologo --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            // El .cobertura.xml se genera aunque haya tests fallando, así que no se mira el exit
            // code: interesa qué está cubierto, no si todo pasa.
            var coberturaPath = Directory
                .EnumerateFiles(resultsDir, "*.cobertura.xml", SearchOption.AllDirectories)
                .FirstOrDefault();

            // Devolver un informe vacío aquí sería peor que fallar: todas las clases parecerían sin
            // cubrir y se generarían tests para la solución entera.
            if (coberturaPath is null)
                return (null, $"El proyecto de tests {testProjectPath} no generó informe de cobertura. ¿Tiene el paquete coverlet.collector? Sin él no se puede medir cobertura real; omite el parámetro para usar la heurística por nombre de clase.");

            var report = CoberturaParser.Parse(await File.ReadAllTextAsync(coberturaPath));
            _logger.LogInformation(
                "Cobertura recogida de {ProjectPath}: {ClassCount} clases con algún método cubierto",
                testProjectPath, report.CoveredMethodsByClass.Count);

            return (report, null);
        }
        finally
        {
            try { Directory.Delete(resultsDir, recursive: true); }
            catch { }
        }
    }
}
