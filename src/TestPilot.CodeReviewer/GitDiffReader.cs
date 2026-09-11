using System.Diagnostics;
using System.Text;

namespace TestPilot.CodeReviewer;

public static class GitDiffReader
{
    public static async Task<string> GetDiffAsync(string repoPath, string? baseBranch = null)
    {
        var arguments = baseBranch is not null ? $"diff {baseBranch}...HEAD" : "diff HEAD";

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repoPath,
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

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git diff falló (exit code {process.ExitCode}): {stderr}");

        return stdout.ToString();
    }
}
