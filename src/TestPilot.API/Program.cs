using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json.Serialization;
using TestPilot.CodeReviewer;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;
using TestPilot.ReportGenerator;
using TestPilot.SourceAnalyzer;
using TestPilot.TestGenerator;
using TestPilot.TestRunner;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Análisis y reporte (sin IA)
builder.Services.AddSingleton<ISourceAnalyzer, SourceAnalyzerService>();
builder.Services.AddSingleton<IReportGenerator, ReportGeneratorService>();

// Proveedor LLM vía endpoint compatible con OpenAI (Groq por defecto; también Ollama, LM Studio,
// OpenRouter...). Un único IChatClient para todos los módulos con IA.
var endpoint = builder.Configuration["AI:Endpoint"] ?? "https://api.groq.com/openai/v1/";
var modelId  = builder.Configuration["AI:ModelId"]  ?? "openai/gpt-oss-120b";

// Los proveedores locales ignoran la clave, pero ApiKeyCredential no admite cadena vacía.
var apiKey = builder.Configuration["AI:ApiKey"] is { Length: > 0 } configuredKey ? configuredKey : "sin-clave";

var openAIClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

builder.Services
    .AddChatClient(openAIClient.GetChatClient(modelId).AsIChatClient())
    .UseLogging();

builder.Services.AddSingleton<ITestGenerator, TestGeneratorService>();
builder.Services.AddSingleton<ITestRunner, TestRunnerService>();
builder.Services.AddSingleton<ICoverageCollector, CoverageCollector>();
builder.Services.AddSingleton<TestCorrectionAgent>();
builder.Services.AddSingleton<ICodeReviewer, CodeReviewerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

// --- Endpoint 1: analizar solución ---
app.MapPost("/api/analyze", async (AnalyzeRequest req, ISourceAnalyzer analyzer, IReportGenerator reporter, ICoverageCollector coverageCollector) =>
{
    if (!File.Exists(req.SolutionPath))
        return Results.BadRequest(new { error = $"Solution file not found: {req.SolutionPath}" });

    // Con TestProjectPath se mide cobertura real; sin él se usa la heurística de nombres.
    CoverageReport? coverage = null;
    if (req.TestProjectPath is not null)
    {
        var (coverageReport, coverageError) = await coverageCollector.CollectAsync(req.TestProjectPath);
        if (coverageError is not null)
            return Results.BadRequest(new { error = coverageError });
        coverage = coverageReport;
    }

    var classes = await analyzer.AnalyzeAsync(req.SolutionPath, req.IncludeInternal, coverage);
    var report  = reporter.Generate(classes, []);
    return Results.Ok(new { classes, report });
});

// --- Endpoint 2: analizar + generar tests con IA (con bucle corrector si se indica TestProjectPath) ---
app.MapPost("/api/generate", async (
    GenerateRequest req,
    ISourceAnalyzer analyzer,
    ITestGenerator generator,
    ITestRunner testRunner,
    TestCorrectionAgent correctionAgent,
    IReportGenerator reporter,
    ICoverageCollector coverageCollector,
    ILogger<Program> logger) =>
{
    if (!File.Exists(req.SolutionPath))
        return Results.BadRequest(new { error = $"Solution file not found: {req.SolutionPath}" });

    if (req.TestProjectPath is not null)
    {
        var pathError = TestOutputPathValidator.Validate(req.OutputPath, req.TestProjectPath);
        if (pathError is not null)
            return Results.BadRequest(new { error = pathError });
    }

    if (req.UseCoverage && req.TestProjectPath is null)
        return Results.BadRequest(new { error = "'useCoverage' requiere 'testProjectPath': la cobertura se mide ejecutando los tests de ese proyecto." });

    // UseCoverage exige TestProjectPath: medir cobertura es ejecutar sus tests.
    CoverageReport? coverage = null;
    if (req.UseCoverage && req.TestProjectPath is not null)
    {
        var (coverageReport, coverageError) = await coverageCollector.CollectAsync(req.TestProjectPath);
        if (coverageError is not null)
            return Results.BadRequest(new { error = coverageError });
        coverage = coverageReport;
    }

    var classes = await analyzer.AnalyzeAsync(req.SolutionPath, req.IncludeInternal, coverage);

    var candidates = classes.AsEnumerable();
    if (req.ClassNames is { Count: > 0 })
    {
        var unknown = req.ClassNames.Where(name => classes.All(c => c.FullName != name)).ToList();
        if (unknown.Count > 0)
            return Results.BadRequest(new { error = $"Clases no encontradas en el análisis: {string.Join(", ", unknown)}" });

        candidates = classes.Where(c => req.ClassNames.Contains(c.FullName));
    }

    var untested  = candidates.Where(c => c.PublicMethods.Any(m => !m.HasTests)).ToList();
    var generated = new List<GeneratedTest>();

    // Se detecta una vez, no por clase: es una propiedad del proyecto de tests, no de la clase.
    var mockingLibrary = req.TestProjectPath is null
        ? null
        : MockingLibraryDetector.Detect(req.TestProjectPath);
    if (mockingLibrary is not null)
        logger.LogInformation("El proyecto de tests referencia {Library}; se permitirá para clases concretas externas", mockingLibrary);

    Directory.CreateDirectory(req.OutputPath);

    foreach (var cls in untested)
    {
        if (generated.Count > 0)
            await Task.Delay(TimeSpan.FromSeconds(5));

        logger.LogInformation("Generando test para {ClassName}...", cls.FullName);
        var test = await generator.GenerateAsync(cls, mockingLibrary);

        var filePath = Path.Combine(req.OutputPath, $"{test.ClassName}.cs");
        await File.WriteAllTextAsync(filePath, test.TestCode);

        if (req.TestProjectPath is not null)
        {
            var runResult = await testRunner.RunAsync(req.TestProjectPath, TestClassNaming.TestClassFilter(cls.FullName));
            if (!runResult.Success)
                test = await correctionAgent.CorrectAsync(test, runResult, cls, filePath, req.TestProjectPath, mockingLibrary);
        }

        generated.Add(test);
    }

    var report = reporter.Generate(classes, generated);
    return Results.Ok(new { report, generatedFiles = generated.Select(g => new { g.ClassName, g.HasErrors }) });
});

// --- Endpoint 3: revisar el diff pendiente de un repositorio git ---
app.MapPost("/api/review", async (ReviewRepoRequest req, ICodeReviewer reviewer) =>
{
    if (!Directory.Exists(req.RepoPath))
        return Results.BadRequest(new { error = $"Repository path not found: {req.RepoPath}" });

    var (conventions, conventionsError) = await ConventionsResolver.ResolveAsync(req.Conventions, req.ConventionsPath);
    if (conventionsError is not null)
        return Results.BadRequest(new { error = conventionsError });

    var diff = await GitDiffReader.GetDiffAsync(req.RepoPath, req.BaseBranch);
    if (string.IsNullOrWhiteSpace(diff))
        return Results.Ok(new CodeReviewResult("No hay cambios que revisar.", []));

    var result = await reviewer.ReviewAsync(diff, conventions);
    return Results.Ok(result);
});

// --- Endpoint 4: revisar un diff pasado directamente ---
app.MapPost("/api/review/diff", async (ReviewDiffRequest req, ICodeReviewer reviewer) =>
{
    if (string.IsNullOrWhiteSpace(req.Diff))
        return Results.BadRequest(new { error = "El campo 'diff' no puede estar vacío." });

    var (conventions, conventionsError) = await ConventionsResolver.ResolveAsync(req.Conventions, req.ConventionsPath);
    if (conventionsError is not null)
        return Results.BadRequest(new { error = conventionsError });

    var result = await reviewer.ReviewAsync(req.Diff, conventions);
    return Results.Ok(result);
});

app.Run();

record AnalyzeRequest(string SolutionPath, bool IncludeInternal = false, string? TestProjectPath = null);
record GenerateRequest(string SolutionPath, string OutputPath, string? TestProjectPath = null, IReadOnlyList<string>? ClassNames = null, bool IncludeInternal = false, bool UseCoverage = false);
record ReviewRepoRequest(string RepoPath, string? BaseBranch = null, string? Conventions = null, string? ConventionsPath = null);
record ReviewDiffRequest(string Diff, string? Conventions = null, string? ConventionsPath = null);
