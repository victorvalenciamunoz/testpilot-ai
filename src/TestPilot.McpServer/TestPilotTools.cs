using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using TestPilot.CodeReviewer;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;
using TestPilot.TestGenerator;

namespace TestPilot.McpServer;

[McpServerToolType]
public sealed class TestPilotTools
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ISourceAnalyzer _analyzer;
    private readonly ITestGenerator _generator;
    private readonly ITestRunner _testRunner;
    private readonly ICoverageCollector _coverageCollector;
    private readonly ICodeReviewer _reviewer;
    private readonly IReportGenerator _reporter;
    private readonly TestCorrectionAgent _correctionAgent;
    private readonly ILogger<TestPilotTools> _logger;

    public TestPilotTools(
        ISourceAnalyzer analyzer,
        ITestGenerator generator,
        ITestRunner testRunner,
        ICoverageCollector coverageCollector,
        ICodeReviewer reviewer,
        IReportGenerator reporter,
        TestCorrectionAgent correctionAgent,
        ILogger<TestPilotTools> logger)
    {
        _analyzer = analyzer;
        _generator = generator;
        _testRunner = testRunner;
        _coverageCollector = coverageCollector;
        _reviewer = reviewer;
        _reporter = reporter;
        _correctionAgent = correctionAgent;
        _logger = logger;
    }

    [McpServerTool(Name = "analyze_solution")]
    [Description("Analiza una solución .NET con Roslyn y devuelve las clases y métodos públicos, indicando cuáles ya tienen tests y cuáles no.")]
    public async Task<string> AnalyzeSolutionAsync(
        [Description("Ruta absoluta al archivo .sln o .slnx de la solución a analizar")] string solutionPath,
        [Description("Incluir también clases internal. Útil en arquitecturas CQRS donde los handlers son internal. Por defecto solo se analizan las públicas.")] bool includeInternal = false,
        [Description("Ruta al proyecto de tests. Si se indica, se ejecutan sus tests con coverlet y el resultado dice qué métodos están cubiertos DE VERDAD, por método. Si se omite se usa una heurística por nombre de clase, mucho menos fiable (marca como testeada una clase solo porque exista otra con nombre parecido acabado en Tests). Requiere el paquete coverlet.collector en el proyecto de tests y tarda más, porque compila y ejecuta.")] string? testProjectPath = null)
    {
        if (!File.Exists(solutionPath))
            return Error($"No se encuentra el archivo de solución: {solutionPath}");

        CoverageReport? coverage = null;
        if (testProjectPath is not null)
        {
            var (coverageReport, coverageError) = await _coverageCollector.CollectAsync(testProjectPath);
            if (coverageError is not null)
                return Error(coverageError);
            coverage = coverageReport;
        }

        _logger.LogInformation("Analizando {SolutionPath}...", solutionPath);
        var classes = await _analyzer.AnalyzeAsync(solutionPath, includeInternal, coverage);
        var report  = _reporter.Generate(classes, []);

        return Serialize(new
        {
            report.TotalClasses,
            report.ClassesWithTests,
            Classes = classes
        });
    }

    [McpServerTool(Name = "generate_tests")]
    [Description("Genera un archivo de tests xUnit para una clase concreta de una solución .NET y lo escribe en disco. Si se indica testProjectPath, ejecuta el test generado y lo corrige automáticamente hasta 3 veces mientras falle.")]
    public async Task<string> GenerateTestsAsync(
        [Description("Ruta absoluta al archivo .sln o .slnx de la solución")] string solutionPath,
        [Description("Nombre completo de la clase objetivo (namespace.ClassName), tal cual lo devuelve analyze_solution")] string className,
        [Description("Carpeta donde escribir el archivo de tests generado")] string outputPath,
        [Description("Ruta al proyecto xUnit ya existente donde ejecutar los tests. Si se omite, el test se genera pero no se ejecuta ni se corrige.")] string? testProjectPath = null,
        [Description("Incluir también clases internal al buscar la clase objetivo")] bool includeInternal = false)
    {
        if (!File.Exists(solutionPath))
            return Error($"No se encuentra el archivo de solución: {solutionPath}");

        if (testProjectPath is not null)
        {
            var pathError = TestOutputPathValidator.Validate(outputPath, testProjectPath);
            if (pathError is not null)
                return Error(pathError);
        }

        var classes = await _analyzer.AnalyzeAsync(solutionPath, includeInternal);
        var target  = classes.FirstOrDefault(c => c.FullName == className);
        if (target is null)
            return Error($"Clase no encontrada en el análisis: {className}. Usa analyze_solution para ver los nombres disponibles.");

        // Propiedad del proyecto de tests, no de la clase: se detecta una vez.
        var mockingLibrary = testProjectPath is null
            ? null
            : MockingLibraryDetector.Detect(testProjectPath);

        _logger.LogInformation("Generando test para {ClassName}...", target.FullName);
        var test = await _generator.GenerateAsync(target, mockingLibrary);

        Directory.CreateDirectory(outputPath);
        var filePath = Path.Combine(outputPath, $"{test.ClassName}.cs");
        await File.WriteAllTextAsync(filePath, test.TestCode);

        if (testProjectPath is not null)
        {
            var runResult = await _testRunner.RunAsync(testProjectPath, TestClassNaming.TestClassFilter(target.FullName));
            if (!runResult.Success)
                test = await _correctionAgent.CorrectAsync(test, runResult, target, filePath, testProjectPath, mockingLibrary);
        }

        return Serialize(new
        {
            test.ClassName,
            test.TargetClass,
            test.HasErrors,
            FilePath = filePath
        });
    }

    [McpServerTool(Name = "run_tests")]
    [Description("Ejecuta dotnet test sobre un proyecto de tests y devuelve cuántos han pasado, cuántos han fallado y los errores de compilación o de ejecución encontrados.")]
    public async Task<string> RunTestsAsync(
        [Description("Ruta al proyecto .csproj de tests, o a la carpeta que lo contiene")] string projectPath)
    {
        if (!File.Exists(projectPath) && !Directory.Exists(projectPath))
            return Error($"No se encuentra el proyecto de tests: {projectPath}");

        _logger.LogInformation("Ejecutando tests de {ProjectPath}...", projectPath);
        var result = await _testRunner.RunAsync(projectPath);
        return Serialize(result);
    }

    [McpServerTool(Name = "review_code")]
    [Description("Revisa un git diff en formato unified y devuelve findings estructurados (severidad, archivo, línea, categoría, descripción y sugerencia) sobre posibles bugs y problemas de calidad. Acepta opcionalmente las convenciones del proyecto para que la revisión se ajuste a sus reglas.")]
    public async Task<string> ReviewCodeAsync(
        [Description("Contenido del git diff a revisar, en formato unified diff")] string diff,
        [Description("Convenciones del proyecto que requieren JUICIO, para aplicarlas además de las reglas genéricas: responsabilidades de cada capa o clase, abstracciones que sobran (YAGNI), manejo de errores acordado, expresividad de los nombres, qué debe probar un test. Texto libre. NO uses esto para estilo, formato o nomenclatura mecánica (lo cubre .editorconfig y los analizadores de Roslyn) ni para reglas de dependencias entre capas (para eso están NetArchTest o ArchUnitNET): ahí un analizador es exacto y esto no. Los incumplimientos se devuelven con categoría 'Convention'. Excluyente con conventionsPath.")] string? conventions = null,
        [Description("Ruta a un archivo con las convenciones del proyecto (CONTRIBUTING.md, una guía de estilo, etc.). Preferible a conventions cuando el archivo ya está en disco: lo lee el servidor, así que su contenido no ocupa tu contexto. Si la ruta no existe se devuelve error en vez de revisar sin convenciones. Excluyente con conventions.")] string? conventionsPath = null)
    {
        if (string.IsNullOrWhiteSpace(diff))
            return Error("El diff está vacío, no hay nada que revisar.");

        var (resolvedConventions, conventionsError) = await ConventionsResolver.ResolveAsync(conventions, conventionsPath);
        if (conventionsError is not null)
            return Error(conventionsError);

        _logger.LogInformation("Revisando diff de {Length} caracteres...", diff.Length);
        var review = await _reviewer.ReviewAsync(diff, resolvedConventions);
        return Serialize(review);
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, _jsonOptions);

    private static string Error(string message) => Serialize(new { error = message });
}
