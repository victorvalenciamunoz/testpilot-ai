using TestPilot.TestGenerator;

namespace TestPilot.TestGenerator.Tests;

public class MockingLibraryDetectorTests : IDisposable
{
    private readonly string _root;
    private readonly string _projectDir;

    public MockingLibraryDetectorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"testpilot-mocklib-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_root, "tests");
        Directory.CreateDirectory(_projectDir);
    }

    private string WriteCsproj(string packages)
    {
        var path = Path.Combine(_projectDir, "Sample.Tests.csproj");
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
                {packages}
              </ItemGroup>
            </Project>
            """);
        return path;
    }

    [Fact]
    public void Detect_SinLibreriaDeMocking_DevuelveNull()
    {
        WriteCsproj("");

        Assert.Null(MockingLibraryDetector.Detect(_projectDir));
    }

    [Fact]
    public void Detect_ConMoq_LoDevuelve()
    {
        WriteCsproj("""<PackageReference Include="Moq" Version="4.20.72" />""");

        Assert.Equal("Moq", MockingLibraryDetector.Detect(_projectDir));
    }

    [Fact]
    public void Detect_ConNSubstitute_LoDevuelve()
    {
        WriteCsproj("""<PackageReference Include="NSubstitute" Version="5.3.0" />""");

        Assert.Equal("NSubstitute", MockingLibraryDetector.Detect(_projectDir));
    }

    [Fact]
    public void Detect_RutaAlCsprojEnVezDeLaCarpeta_FuncionaIgual()
    {
        var csproj = WriteCsproj("""<PackageReference Include="Moq" Version="4.20.72" />""");

        Assert.Equal("Moq", MockingLibraryDetector.Detect(csproj));
    }

    [Fact]
    public void Detect_PaqueteConPrefijoComun_NoSeConfundeConLaLibreria()
    {
        // "Moq.AutoMock" no es "Moq": el nombre tiene que coincidir entero.
        WriteCsproj("""<PackageReference Include="Moq.AutoMock" Version="3.5.0" />""");

        Assert.Null(MockingLibraryDetector.Detect(_projectDir));
    }

    [Fact]
    public void Detect_DeclaradoEnDirectoryPackagesPropsDelPadre_LoEncuentra()
    {
        WriteCsproj("");
        File.WriteAllText(Path.Combine(_root, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="NSubstitute" Version="5.3.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal("NSubstitute", MockingLibraryDetector.Detect(_projectDir));
    }

    [Fact]
    public void Detect_ProyectoInexistente_DevuelveNull()
    {
        Assert.Null(MockingLibraryDetector.Detect(Path.Combine(_root, "no-existe")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { }
    }
}
