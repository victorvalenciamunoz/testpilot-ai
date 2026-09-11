using TestPilot.Core.Text;

namespace TestPilot.CodeReviewer.Tests;

// CompilerErrorFilter vive en Core; se prueba aquí porque Core no tiene proyecto de tests propio,
// igual que el resto de helpers compartidos.
public class CompilerErrorFilterTests
{
    private static readonly string _generated = Path.Combine(Path.GetTempPath(), "Generated", "CalculatorTests.cs");
    private static readonly string _ajeno = Path.Combine(Path.GetTempPath(), "Generated", "LegacyTests.cs");

    [Fact]
    public void ErrorsFrom_ErrorDelArchivoIndicado_LoConserva()
    {
        var errors = new[] { $"{_generated}(12,9): CS0103: The name 'foo' does not exist" };

        var own = CompilerErrorFilter.ErrorsFrom(errors, _generated);

        Assert.Single(own);
    }

    [Fact]
    public void ErrorsFrom_ErrorDeOtroArchivo_LoDescarta()
    {
        var errors = new[] { $"{_ajeno}(3,5): CS1002: ; expected" };

        var own = CompilerErrorFilter.ErrorsFrom(errors, _generated);

        Assert.Empty(own);
    }

    [Fact]
    public void ErrorsFrom_MezclaDeArchivos_SoloDevuelveLosPropios()
    {
        var errors = new[]
        {
            $"{_ajeno}(3,5): CS1002: ; expected",
            $"{_generated}(12,9): CS0103: The name 'foo' does not exist",
            $"{_ajeno}(9,1): CS0246: type not found"
        };

        var own = CompilerErrorFilter.ErrorsFrom(errors, _generated);

        Assert.Single(own);
        Assert.Contains("CS0103", own[0]);
    }

    [Fact]
    public void ErrorsFrom_FalloDeTestEnEjecucion_SeConsideraPropio()
    {
        // No tiene forma de error del compilador: la ejecución ya viene acotada con --filter.
        var errors = new[] { "[FAIL] SampleProject.Tests.CalculatorTests.Add_DosNumeros_DevuelveLaSuma" };

        var own = CompilerErrorFilter.ErrorsFrom(errors, _generated);

        Assert.Single(own);
    }

    [Fact]
    public void ErrorsFrom_SinErrores_DevuelveListaVacia()
    {
        Assert.Empty(CompilerErrorFilter.ErrorsFrom([], _generated));
    }
}
