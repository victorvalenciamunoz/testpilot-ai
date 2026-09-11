using TestPilot.CodeReviewer;

namespace TestPilot.CodeReviewer.Tests;

public class ConventionsResolverTests : IDisposable
{
    private readonly string _tempDir;

    public ConventionsResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"testpilot-conventions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ResolveAsync_SinNada_DevuelveNullSinError()
    {
        var (text, error) = await ConventionsResolver.ResolveAsync(null, null);

        Assert.Null(text);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveAsync_SoloTexto_DevuelveEseTexto()
    {
        var (text, error) = await ConventionsResolver.ResolveAsync("- Namespaces file-scoped", null);

        Assert.Equal("- Namespaces file-scoped", text);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveAsync_SoloRuta_DevuelveElContenidoDelArchivo()
    {
        var path = Path.Combine(_tempDir, "convenciones.md");
        await File.WriteAllTextAsync(path, "- Servicios sealed\n- Campos _camelCase");

        var (text, error) = await ConventionsResolver.ResolveAsync(null, path);

        Assert.Equal("- Servicios sealed\n- Campos _camelCase", text);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveAsync_TextoYRutaALaVez_DevuelveError()
    {
        var path = Path.Combine(_tempDir, "convenciones.md");
        await File.WriteAllTextAsync(path, "- Servicios sealed");

        var (text, error) = await ConventionsResolver.ResolveAsync("- Otra cosa", path);

        Assert.Null(text);
        Assert.Contains("no ambos", error);
    }

    [Fact]
    public async Task ResolveAsync_RutaInexistente_DevuelveErrorEnVezDeIgnorarla()
    {
        var path = Path.Combine(_tempDir, "no-existe.md");

        var (text, error) = await ConventionsResolver.ResolveAsync(null, path);

        Assert.Null(text);
        Assert.Contains("No se encuentra el archivo de convenciones", error);
    }

    [Fact]
    public async Task ResolveAsync_TextoEnBlanco_SeTrataComoAusente()
    {
        var (text, error) = await ConventionsResolver.ResolveAsync("   ", null);

        Assert.Null(text);
        Assert.Null(error);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }
}
