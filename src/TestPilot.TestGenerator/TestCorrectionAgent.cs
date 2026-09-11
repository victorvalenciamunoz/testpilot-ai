using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;
using TestPilot.Core.Text;

namespace TestPilot.TestGenerator;

public sealed class TestCorrectionAgent
{
    private const int MaxAttempts = 3;

    private static readonly string _promptTemplate =
        PromptResourceLoader.LoadEmbeddedPrompt("TestPilot.TestGenerator.Prompts.CorrectTest.txt");

    // 0 también al corregir: se quiso subirla para evitar que el bucle repitiera un intento
    // idéntico, pero el proveedor ya varía de por sí a temperatura 0, así que ese riesgo no existe
    // y subirla solo aumentaría la probabilidad de inventar APIs cuando más precisión hace falta.
    private static readonly ChatOptions _chatOptions = new() { Temperature = 0 };

    private readonly IChatClient _chatClient;
    private readonly ITestRunner _testRunner;
    private readonly ILogger<TestCorrectionAgent> _logger;

    public TestCorrectionAgent(IChatClient chatClient, ITestRunner testRunner, ILogger<TestCorrectionAgent> logger)
    {
        _chatClient = chatClient;
        _testRunner = testRunner;
        _logger = logger;
    }

    // Bucle agente: cada intento le pasa al LLM el código actual + los errores del intento
    // anterior, para que la corrección se apoye en contexto acumulado en vez de regenerar desde cero.
    public async Task<GeneratedTest> CorrectAsync(
        GeneratedTest failedTest,
        TestRunResult runResult,
        ClassInfo originalClass,
        string testFilePath,
        string projectPath,
        string? mockingLibrary = null)
    {
        var currentCode = failedTest.TestCode;
        var currentResult = runResult;
        var testClassFilter = TestClassNaming.TestClassFilter(originalClass.FullName);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // Si el proyecto de tests falla por causas ajenas al archivo generado, corregirlo sería
            // reescribirlo a partir de errores que no son suyos y agotar los intentos en vano.
            var ownErrors = CompilerErrorFilter.ErrorsFrom(currentResult.Errors, testFilePath);
            if (ownErrors.Count == 0)
            {
                _logger.LogWarning(
                    "Test para {ClassName}: ninguno de los {ErrorCount} errores procede del archivo generado; " +
                    "el proyecto de tests falla por causas ajenas y no se intenta corregir",
                    originalClass.FullName, currentResult.Errors.Count);
                return failedTest with { TestCode = currentCode, HasErrors = true };
            }

            _logger.LogWarning(
                "Test para {ClassName} falló con {ErrorCount} errores propios (intento {Attempt}/{MaxAttempts}), corrigiendo...",
                originalClass.FullName, ownErrors.Count, attempt, MaxAttempts);

            var prompt = _promptTemplate
                .Replace("{{$testCode}}", currentCode)
                .Replace("{{$errors}}", string.Join("\n", ownErrors))
                .Replace("{{$mockingPolicy}}", MockingPolicy.Describe(mockingLibrary));

            var response = await _chatClient.GetResponseAsync(prompt, _chatOptions);
            currentCode = MarkdownFenceStripper.Strip(response.Text);

            await File.WriteAllTextAsync(testFilePath, currentCode);
            currentResult = await _testRunner.RunAsync(projectPath, testClassFilter);

            if (currentResult.Success)
            {
                _logger.LogInformation(
                    "Test para {ClassName} corregido exitosamente en intento {Attempt}/{MaxAttempts}",
                    originalClass.FullName, attempt, MaxAttempts);
                return failedTest with { TestCode = currentCode, HasErrors = false };
            }
        }

        _logger.LogWarning(
            "Test para {ClassName} sigue fallando tras {MaxAttempts} intentos, se conserva el último intento",
            originalClass.FullName, MaxAttempts);
        return failedTest with { TestCode = currentCode, HasErrors = true };
    }
}
