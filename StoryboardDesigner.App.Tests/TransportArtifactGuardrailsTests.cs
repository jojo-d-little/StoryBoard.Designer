using System.Diagnostics;

namespace StoryboardDesigner.App.Tests;

public sealed class TransportArtifactGuardrailsTests
{
    [Fact]
    public void TransportCodegenVerify_Passes_WhenGeneratedArtifactsAreCurrent()
    {
        var repoRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project .\\Storyboard.TransportCodegen\\Storyboard.TransportCodegen.csproj -- verify",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();

        var exited = process.WaitForExit(120000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            Assert.Fail("Transport artifact verification timed out after 120 seconds.");
        }

        var details =
            $"ExitCode={process.ExitCode}{Environment.NewLine}"
            + $"STDOUT:{Environment.NewLine}{standardOutput}{Environment.NewLine}"
            + $"STDERR:{Environment.NewLine}{standardError}";

        Assert.True(process.ExitCode == 0, "Transport artifact verification failed. " + details);
        Assert.DoesNotContain("stale", standardError, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, "StoryboardDesigner.slnx");
            if (File.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test execution directory.");
    }
}