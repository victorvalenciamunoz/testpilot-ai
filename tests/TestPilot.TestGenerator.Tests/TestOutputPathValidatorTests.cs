using TestPilot.TestGenerator;

namespace TestPilot.TestGenerator.Tests;

public class TestOutputPathValidatorTests : IDisposable
{
    private readonly string _projectDir;

    public TestOutputPathValidatorTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"testpilot-outpath-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);
    }

    [Fact]
    public void Validate_OutputIgualAlProyecto_NoDevuelveError()
    {
        Assert.Null(TestOutputPathValidator.Validate(_projectDir, _projectDir));
    }

    [Fact]
    public void Validate_OutputDentroDelProyecto_NoDevuelveError()
    {
        var output = Path.Combine(_projectDir, "Generated");

        Assert.Null(TestOutputPathValidator.Validate(output, _projectDir));
    }

    [Fact]
    public void Validate_TestProjectPathEsUnCsproj_UsaSuCarpeta()
    {
        var csproj = Path.Combine(_projectDir, "MisTests.csproj");
        File.WriteAllText(csproj, "<Project />");
        var output = Path.Combine(_projectDir, "Generated");

        Assert.Null(TestOutputPathValidator.Validate(output, csproj));
    }

    [Fact]
    public void Validate_OutputFueraDelProyecto_DevuelveError()
    {
        var output = Path.Combine(Path.GetTempPath(), $"otra-carpeta-{Guid.NewGuid():N}");

        var error = TestOutputPathValidator.Validate(output, _projectDir);

        Assert.Contains("está fuera del proyecto de tests", error);
    }

    [Fact]
    public void Validate_OutputHermanoConPrefijoComun_DevuelveError()
    {
        // "proyecto-extra" empieza por "proyecto" pero no está dentro: comparar por prefijo de
        // cadena sin el separador daría un falso positivo.
        var output = _projectDir + "-extra";

        var error = TestOutputPathValidator.Validate(output, _projectDir);

        Assert.Contains("está fuera del proyecto de tests", error);
    }

    [Fact]
    public void Validate_RutasEquivalentesConSeparadorFinal_NoDevuelveError()
    {
        var output = _projectDir + Path.DirectorySeparatorChar;

        Assert.Null(TestOutputPathValidator.Validate(output, _projectDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_projectDir, recursive: true); }
        catch { }
    }
}
