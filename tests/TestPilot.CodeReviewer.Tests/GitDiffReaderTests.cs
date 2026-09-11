using System.Diagnostics;
using TestPilot.CodeReviewer;

namespace TestPilot.CodeReviewer.Tests;

// Integración: crea un repo git real en disco para comprobar que GitDiffReader
// invoca `git diff` correctamente y devuelve el contenido esperado.
public class GitDiffReaderTests : IDisposable
{
    private readonly string _tempRepo;

    public GitDiffReaderTests()
    {
        _tempRepo = Path.Combine(Path.GetTempPath(), $"testpilot-git-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRepo);

        RunGit("init");
        RunGit("config user.email test@testpilot.local");
        RunGit("config user.name TestPilot");

        File.WriteAllText(Path.Combine(_tempRepo, "Sample.cs"), "public class Sample\n{\n}\n");
        RunGit("add .");
        RunGit("commit -m initial");
    }

    [Fact]
    public async Task GetDiffAsync_WithUncommittedChange_ReturnsDiffContainingChange()
    {
        File.WriteAllText(Path.Combine(_tempRepo, "Sample.cs"), "public class Sample\n{\n    public void Foo() { }\n}\n");

        var diff = await GitDiffReader.GetDiffAsync(_tempRepo);

        Assert.Contains("Sample.cs", diff);
        Assert.Contains("Foo", diff);
    }

    [Fact]
    public async Task GetDiffAsync_WithNoChanges_ReturnsEmpty()
    {
        var diff = await GitDiffReader.GetDiffAsync(_tempRepo);

        Assert.True(string.IsNullOrWhiteSpace(diff));
    }

    private void RunGit(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _tempRepo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRepo, recursive: true); }
        catch { }
    }
}
