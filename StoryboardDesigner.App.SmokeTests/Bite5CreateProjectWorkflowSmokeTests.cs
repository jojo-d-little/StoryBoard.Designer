using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

public sealed class Bite5CreateProjectWorkflowSmokeTests
{
    [Fact]
    public void CreateNew_Uses_BuiltIn_Starter_And_Updates_Window_Title()
    {
        StaTestRunner.Run(() =>
        {
            var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"storyboard-create-smoke-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            var preferencesPath = GetProjectCreationPreferencesPath();
            var priorPreferencesExisted = File.Exists(preferencesPath);
            var priorPreferencesJson = priorPreferencesExisted ? File.ReadAllText(preferencesPath) : null;

            try
            {
                WriteProjectCreationPreferences(preferencesPath, tempRoot);

                using var app = Application.Launch(executablePath);
                try
                {
                    using var automation = new UIA3Automation();

                    var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);
                    Assert.NotNull(mainWindow);
                    SmokeUiRetryHelpers.AssertAppStillRunning(app, "opening create-project smoke shell");

                    var fileMenu = Retry.WhileNull(
                        () => mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.File"))?.AsMenuItem(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(fileMenu);
                    SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding file menu");
                    fileMenu!.Expand();

                    var createNewMenu = Retry.WhileNull(
                        () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.CreateNewProject"))?.AsMenuItem(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(createNewMenu);
                    createNewMenu!.Invoke();

                    var starterCombo = SmokeUiRetryHelpers.WaitForDesktopElementByAutomationId(automation, "CreateProjectDialog.StarterCombo")?.AsComboBox();
                    Assert.NotNull(starterCombo);

                    var starterItems = starterCombo!.Items;
                    Assert.True(starterItems.Length >= 2, "Expected starter combo to include Blank and at least one built-in starter.");
                    starterItems[1].Select();

                    var projectName = $"SmokeCreate_{DateTime.Now:HHmmssfff}";
                    var projectNameTextBox = SmokeUiRetryHelpers.WaitForDesktopElementByAutomationId(automation, "CreateProjectDialog.ProjectName")?.AsTextBox();
                    Assert.NotNull(projectNameTextBox);
                    projectNameTextBox!.Text = projectName;

                    var submitButton = SmokeUiRetryHelpers.WaitForDesktopElementByAutomationId(automation, "CreateProjectDialog.Submit")?.AsButton();
                    Assert.NotNull(submitButton);
                    SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding create dialog submit button");
                    submitButton!.Invoke();

                    var dialogClosed = Retry.WhileFalse(
                        () => SmokeUiRetryHelpers.TryFindDesktopElementByAutomationId(automation, "CreateProjectDialog.Submit") is null,
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.True(dialogClosed, "Create Project dialog did not close after submit.");

                    var titleUpdated = Retry.WhileFalse(
                        () => string.Equals(
                            SmokeUiRetryHelpers.TryGetWindowTitle(mainWindow),
                            $"Storyboard Designer - {projectName}",
                            StringComparison.Ordinal),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.True(titleUpdated, "Main window title did not update to the new project name.");

                    var createdProjectPath = Path.Combine(tempRoot, projectName, $"{projectName}.sbe.json");
                    Assert.True(File.Exists(createdProjectPath), $"Expected created project file was not found: {createdProjectPath}");

                    SmokeUiRetryHelpers.CloseAppWindowOrKill(
                        app,
                        mainWindow,
                        automation,
                        "Designer process did not exit after create workflow smoke test.");
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
                RestoreProjectCreationPreferences(preferencesPath, priorPreferencesExisted, priorPreferencesJson);
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
                catch
                {
                }
            }
        });
    }

    private static string GetProjectCreationPreferencesPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StoryboardDesigner",
            "project-creation.preferences.json");
    }

    private static void WriteProjectCreationPreferences(string preferencesPath, string projectParentFolder)
    {
        var directory = Path.GetDirectoryName(preferencesPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new
        {
            starterProjectsRootOverride = (string?)null,
            lastStarterBrowseFolder = projectParentFolder,
            lastCreateProjectParentFolder = projectParentFolder
        };

        File.WriteAllText(preferencesPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static void RestoreProjectCreationPreferences(string preferencesPath, bool priorExisted, string? priorJson)
    {
        try
        {
            if (priorExisted)
            {
                File.WriteAllText(preferencesPath, priorJson ?? string.Empty);
                return;
            }

            if (File.Exists(preferencesPath))
            {
                File.Delete(preferencesPath);
            }
        }
        catch
        {
        }
    }
}
