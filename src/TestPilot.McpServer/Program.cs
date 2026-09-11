using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using TestPilot.CodeReviewer;
using TestPilot.Core.Interfaces;
using TestPilot.McpServer;
using TestPilot.ReportGenerator;
using TestPilot.SourceAnalyzer;
using TestPilot.TestGenerator;
using TestPilot.TestRunner;

// El cliente MCP arranca el proceso desde un directorio arbitrario: el content root se ancla
// al directorio del ejecutable para que appsettings.json se encuentre siempre.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// La clave se comparte con la API vía user-secrets (mismo UserSecretsId); las variables
// de entorno del cliente MCP la sobrescriben (AI__ApiKey / AI__ModelId).
builder.Configuration.AddUserSecrets<TestPilotTools>(optional: true);

// El transporte stdio usa stdout para el protocolo JSON-RPC: cualquier log escrito ahí
// corrompe la sesión MCP, así que todo el logging va a stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

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

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TestPilotTools>();

await builder.Build().RunAsync();
