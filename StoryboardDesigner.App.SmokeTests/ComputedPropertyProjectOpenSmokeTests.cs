using System.Text.Json;
using FlaUI.Core;
using FlaUI.UIA3;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace StoryboardDesigner.App.SmokeTests;

[Collection("Designer UI Smoke")]
public sealed class ComputedPropertyProjectOpenSmokeTests
{
    private const string ProjectPathsEnvironmentVariable = "STORYBOARD_COMPUTED_PROPERTY_ROUNDTRIP_PROJECTS";
    private const string SmokeProjectPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_PROJECT_PATH";
    private readonly ITestOutputHelper _output;

    public ComputedPropertyProjectOpenSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> ProjectPaths => ReadConfiguredProjectPaths()
        .DefaultIfEmpty(string.Empty)
        .Select(static path => new object[] { path });

    [Theory]
    [MemberData(nameof(ProjectPaths))]
    public void OpensConfiguredProjectThroughWpfStartup(string sourceProjectFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectFilePath))
        {
            throw SkipException.ForSkip($"Set {ProjectPathsEnvironmentVariable} to project file paths to run the Stage 05 WPF open smoke test.");
        }

        sourceProjectFilePath = Path.GetFullPath(sourceProjectFilePath);
        var projectName = ReadProjectName(sourceProjectFilePath);
        var sourceProjectFolder = Path.GetDirectoryName(sourceProjectFilePath)
            ?? throw new InvalidOperationException($"[{projectName}] Project path has no parent folder.");
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.SmokeTests", "ComputedPropertyProjectOpen", Guid.NewGuid().ToString("N"));
        var isolatedProjectFolder = Path.Combine(tempRoot, Path.GetFileName(sourceProjectFolder));
        CopyDirectoryRecursive(sourceProjectFolder, isolatedProjectFolder);
        var isolatedProjectFilePath = Path.Combine(isolatedProjectFolder, Path.GetFileName(sourceProjectFilePath));

        try
        {
            StaTestRunner.Run(() =>
            {
                var priorSmokeProjectPath = Environment.GetEnvironmentVariable(SmokeProjectPathEnvironmentVariable);
                Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, isolatedProjectFilePath);
                try
                {
                    var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();
                    using var app = Application.Launch(executablePath);
                    try
                    {
                        using var automation = new UIA3Automation();
                        var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);
                        Assert.NotNull(mainWindow);
                        var expectedTitle = $"Storyboard Designer - {projectName}";
                        var projectWindow = FlaUI.Core.Tools.Retry.WhileNull(
                            () => app.GetAllTopLevelWindows(automation)
                                .FirstOrDefault(window => string.Equals(window.Title, expectedTitle, StringComparison.Ordinal)),
                            timeout: TimeSpan.FromSeconds(60),
                            interval: TimeSpan.FromMilliseconds(100),
                            throwOnTimeout: false).Result;
                        var openWindowTitles = string.Join(", ", app.GetAllTopLevelWindows(automation)
                            .Select(static window => $"'{window.Title}'"));
                        Assert.NotNull(projectWindow);
                        Assert.Equal(expectedTitle, projectWindow!.Title);
                        SmokeUiRetryHelpers.AssertAppStillRunning(app, $"opening project '{projectName}'");
                        SmokeUiRetryHelpers.CloseAppWindowOrKill(
                            app,
                            projectWindow,
                            automation,
                            $"[{projectName}] Designer did not exit after the project-open smoke test.");
                    }
                    finally
                    {
                        if (!app.HasExited)
                        {
                            app.Kill();
                        }
                    }
                }
                finally
                {
                    Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, priorSmokeProjectPath);
                }
            });

            _output.WriteLine($"PASS [{projectName}] WPF startup opened an isolated copy at '{isolatedProjectFilePath}'.");
        }
        catch (Exception exception)
        {
            _output.WriteLine($"FAIL [{projectName}] {exception}");
            throw;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static IEnumerable<string> ReadConfiguredProjectPaths()
    {
        var configured = Environment.GetEnvironmentVariable(ProjectPathsEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configured)
            ? Array.Empty<string>()
            : configured.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ReadProjectName(string projectFilePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(projectFilePath));
        return document.RootElement.TryGetProperty("name", out var name)
            ? name.GetString() ?? Path.GetFileNameWithoutExtension(projectFilePath)
            : Path.GetFileNameWithoutExtension(projectFilePath);
    }

    private static void CopyDirectoryRecursive(string sourceFolder, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        foreach (var filePath in Directory.EnumerateFiles(sourceFolder))
        {
            File.Copy(filePath, Path.Combine(destinationFolder, Path.GetFileName(filePath)), overwrite: true);
        }

        foreach (var childFolder in Directory.EnumerateDirectories(sourceFolder))
        {
            CopyDirectoryRecursive(childFolder, Path.Combine(destinationFolder, Path.GetFileName(childFolder)));
        }
    }
}
