using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;

namespace TestPilot.TestRunner;

public sealed class TestRunnerService : ITestRunner
{
    // El texto de consola de `dotnet test` viene localizado según el idioma del sistema
    // (p.ej. "Con error: 1, Superado: 3" en vez de "Failed: 1, Passed: 3"), así que los
    // recuentos se leen del .trx (XML), no del texto. Los prefijos "error CS"/"warning CS"
    // del compilador y el "[FAIL]" de xUnit sí son estables entre idiomas.
    private static readonly XNamespace TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private readonly ILogger<TestRunnerService> _logger;

    public TestRunnerService(ILogger<TestRunnerService> logger) => _logger = logger;

    public async Task<TestRunResult> RunAsync(string projectPath, string? testClassFilter = null)
    {
        var resultsDir = Path.Combine(Path.GetTempPath(), $"testpilot-trx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(resultsDir);

        // Acotar la ejecución a la clase generada evita que un test roto preexistente del proyecto
        // marque el resultado como fallido y contamine el bucle corrector.
        var filterArg = string.IsNullOrWhiteSpace(testClassFilter)
            ? string.Empty
            : $" --filter \"FullyQualifiedName~{testClassFilter}\"";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --nologo{filterArg} --logger \"trx;LogFileName=results.trx\" --results-directory \"{resultsDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            var output = stdout.ToString();
            var combinedOutput = output + Environment.NewLine + stderr;

            var compilerErrors = BuildErrorParser.ParseCompilerErrors(combinedOutput);
            var (passed, failed) = ParseTrxCounters(resultsDir);
            var errors = compilerErrors.Count > 0 ? compilerErrors : ParseTestFailures(output);
            var success = process.ExitCode == 0 && compilerErrors.Count == 0;

            _logger.LogInformation(
                "dotnet test finalizado (exit code {ExitCode}): {Passed} passed, {Failed} failed",
                process.ExitCode, passed, failed);

            return new TestRunResult(success, passed, failed, errors);
        }
        finally
        {
            try { Directory.Delete(resultsDir, recursive: true); }
            catch { }
        }
    }

    private static (int Passed, int Failed) ParseTrxCounters(string resultsDir)
    {
        var trxPath = Directory.EnumerateFiles(resultsDir, "*.trx").FirstOrDefault();
        if (trxPath is null)
            return (0, 0);

        var counters = XDocument.Load(trxPath).Root?.Element(TrxNamespace + "ResultSummary")?.Element(TrxNamespace + "Counters");
        if (counters is null)
            return (0, 0);

        return ((int?)counters.Attribute("passed") ?? 0, (int?)counters.Attribute("failed") ?? 0);
    }

    private static IReadOnlyList<string> ParseTestFailures(string output) =>
        output.Split('\n')
            .Where(line => line.Contains("[FAIL]", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim())
            .ToList();
}
