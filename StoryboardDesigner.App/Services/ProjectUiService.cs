using System.Windows;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Services;

public sealed class ProjectUiService : IProjectUiService
{
    private const string SmokeOutputLogPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_OUTPUT_LOG_PATH";
    private readonly IProjectCreationPreferencesService _projectCreationPreferencesService;
    private ValidationReportWindow? _validationReportWindow;

    public ProjectUiService(IProjectCreationPreferencesService projectCreationPreferencesService)
    {
        _projectCreationPreferencesService = projectCreationPreferencesService;
    }

    public bool TryGetCreateProjectInput(CreateProjectDialogRequest request, out CreateProjectDialogResult result)
    {
        var dialog = new CreateProjectDialog(CreateProjectDialogMode.Create, request)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            result = new CreateProjectDialogResult
            {
                ProjectFolder = string.Empty,
                ProjectName = string.Empty,
                GameDisplayName = string.Empty,
                GameSummary = string.Empty,
                GamePreviewImages = new List<string>(),
                LastStarterBrowseFolder = request.DefaultStarterBrowseFolder
            };
            return false;
        }

        result = new CreateProjectDialogResult
        {
            ProjectFolder = dialog.ProjectFolder,
            ProjectName = dialog.ProjectName,
            GameDisplayName = dialog.GameDisplayName,
            GameSummary = dialog.GameSummary,
            GamePreviewImages = dialog.GamePreviewImages.ToList(),
            StarterProjectFilePath = dialog.StarterProjectFilePath,
            LastStarterBrowseFolder = dialog.LastStarterBrowseFolder
        };
        return true;
    }

    public CreateProjectDialogRequest BuildCreateProjectDialogRequest()
    {
        var preferences = _projectCreationPreferencesService.Load();
        var starterProjectsRoot = ResolveStarterProjectsRoot(preferences.StarterProjectsRootOverride);
        var defaultProjectFolder = ResolveDefaultProjectFolder(preferences.LastCreateProjectParentFolder);
        var defaultStarterBrowseFolder = ResolveDefaultStarterBrowseFolder(preferences.LastStarterBrowseFolder, starterProjectsRoot);

        return new CreateProjectDialogRequest
        {
            DefaultProjectFolder = defaultProjectFolder,
            DefaultGameDisplayName = string.Empty,
            DefaultGameSummary = string.Empty,
            DefaultGamePreviewImages = new List<string>(),
            DefaultStarterBrowseFolder = defaultStarterBrowseFolder,
            StarterProjectsRoot = starterProjectsRoot,
            BuiltInStarterProjects = DiscoverStarterProjects(starterProjectsRoot)
        };
    }

    public void SaveCreateProjectDialogPreferences(CreateProjectDialogRequest request, CreateProjectDialogResult result)
    {
        var preferences = _projectCreationPreferencesService.Load();
        preferences.LastCreateProjectParentFolder = result.ProjectFolder;
        preferences.LastStarterBrowseFolder = result.LastStarterBrowseFolder;
        _projectCreationPreferencesService.Save(preferences);
    }

    public bool TryGetSaveAsProjectInput(out string projectFolder, out string projectName)
    {
        var dialog = new CreateProjectDialog(CreateProjectDialogMode.SaveAs)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            projectFolder = string.Empty;
            projectName = string.Empty;
            return false;
        }

        projectFolder = dialog.ProjectFolder;
        projectName = dialog.ProjectName;
        return true;
    }

    public bool TryGetOpenProjectPath(out string projectFilePath)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Storyboard project (*.sbe.json)|*.sbe.json|JSON files (*.json)|*.json",
            Title = "Open Storyboard Project"
        };

        if (dialog.ShowDialog(GetOwnerWindow()) != true)
        {
            projectFilePath = string.Empty;
            return false;
        }

        projectFilePath = dialog.FileName;
        return true;
    }

    public bool TryGetImportGlobalNodeProjectPath(out string projectFilePath)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Storyboard globals (*.sbe.globals.json)|*.sbe.globals.json|JSON files (*.json)|*.json",
            Title = "Select Global Node File To Import"
        };

        if (dialog.ShowDialog(GetOwnerWindow()) != true)
        {
            projectFilePath = string.Empty;
            return false;
        }

        projectFilePath = dialog.FileName;
        return true;
    }

    public bool TryGetImportGlobalNodeSelection(GlobalImportSelectionRequest request, out GlobalImportSelectionResult selection)
    {
        var dialog = new ImportGlobalsDialog(request)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            selection = new GlobalImportSelectionResult();
            return false;
        }

        selection = dialog.Selection;
        return true;
    }

    public bool TryGetValidationRunSelection(ValidationRunDialogRequest request, out ValidationRunDialogResult selection)
    {
        var dialog = new ValidationRunDialog(request)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            selection = new ValidationRunDialogResult();
            return false;
        }

        selection = dialog.Selection;
        return true;
    }

    public void ShowInformation(string message, string title)
    {
        var owner = GetOwnerWindow();
        if (owner is null)
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title)
    {
        var owner = GetOwnerWindow();
        if (owner is null)
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title)
    {
        var owner = GetOwnerWindow();
        if (owner is null)
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool Confirm(string message, string title)
    {
        var owner = GetOwnerWindow();
        var result = owner is null
            ? System.Windows.MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
            : System.Windows.MessageBox.Show(
                owner,
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmContinueWithValidationSummary(string workflowName, int issueCount, int errorCount, int warningCount)
    {
        var normalizedWorkflowName = string.IsNullOrWhiteSpace(workflowName)
            ? "this action"
            : workflowName.Trim();

        var message =
            $"Validation found {issueCount} issue(s): {errorCount} error(s), {warningCount} warning(s).{Environment.NewLine}{Environment.NewLine}"
            + $"Continue {normalizedWorkflowName}?";

        var owner = GetOwnerWindow();
        var result = owner is null
            ? System.Windows.MessageBox.Show(
                message,
                "Validation Summary",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning)
            : System.Windows.MessageBox.Show(
                owner,
                message,
                "Validation Summary",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmContinueSaveWithValidation(int errorCount, int warningCount, string reportFilePath)
    {
        var message =
            $"Validation found {errorCount} error(s) and {warningCount} warning(s).{Environment.NewLine}{Environment.NewLine}"
            + "A validation report was written to:" + Environment.NewLine
            + reportFilePath + Environment.NewLine + Environment.NewLine
            + "Save anyway?";

        var owner = GetOwnerWindow();
        var result = owner is null
            ? System.Windows.MessageBox.Show(
                message,
                "Save With Validation Issues",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning)
            : System.Windows.MessageBox.Show(
                owner,
                message,
                "Save With Validation Issues",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    public void ShowValidationReport(
        string reportTitle,
        IReadOnlyList<ProjectValidationIssue> issues,
        string reportFilePath,
        Action<ProjectValidationIssue>? navigateToIssue = null)
    {
        if (_validationReportWindow is null || !_validationReportWindow.IsLoaded)
        {
            _validationReportWindow = new ValidationReportWindow(reportTitle, issues, reportFilePath, navigateToIssue)
            {
                Owner = GetOwnerWindow()
            };
            _validationReportWindow.Closed += (_, _) => _validationReportWindow = null;
            _validationReportWindow.Show();
            return;
        }

        _validationReportWindow.Close();
        _validationReportWindow = new ValidationReportWindow(reportTitle, issues, reportFilePath, navigateToIssue)
        {
            Owner = GetOwnerWindow()
        };
        _validationReportWindow.Closed += (_, _) => _validationReportWindow = null;
        _validationReportWindow.Show();
        _validationReportWindow.Activate();
    }

    public AutoSaveBeforeGameStateInitResponse PromptAutoSaveBeforeGameStateInitialization()
    {
        var dialog = new GameStateAutoSavePromptDialog
        {
            Owner = GetOwnerWindow()
        };

        var result = dialog.ShowDialog();
        if (result != true)
        {
            return new AutoSaveBeforeGameStateInitResponse
            {
                Confirmed = false,
                ShouldAutoSave = false,
                DoNotAskAgainThisRun = dialog.DoNotAskAgainThisRun
            };
        }

        return new AutoSaveBeforeGameStateInitResponse
        {
            Confirmed = true,
            ShouldAutoSave = dialog.ShouldAutoSave,
            DoNotAskAgainThisRun = dialog.DoNotAskAgainThisRun
        };
    }

    public bool TryGetRuntimeVariableValue(string variableName, string currentValue, out string value)
    {
        var dialog = new RuntimeVariableValueDialog(variableName, currentValue)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            value = string.Empty;
            return false;
        }

        value = dialog.ValueText;
        return true;
    }

    public bool TryGetSaveGameSimulatorRecordingPath(out string filePath)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Simulator recording (*.sbe.sim.json)|*.sbe.sim.json|JSON files (*.json)|*.json",
            Title = "Save Simulator Recording",
            AddExtension = true,
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog(GetOwnerWindow()) != true)
        {
            filePath = string.Empty;
            return false;
        }

        filePath = dialog.FileName;
        return true;
    }

    public bool TryGetOpenGameSimulatorRecordingPath(out string filePath)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Simulator recording (*.sbe.sim.json)|*.sbe.sim.json|JSON files (*.json)|*.json",
            Title = "Open Simulator Recording"
        };

        if (dialog.ShowDialog(GetOwnerWindow()) != true)
        {
            filePath = string.Empty;
            return false;
        }

        filePath = dialog.FileName;
        return true;
    }

    public bool TryGetSaveOutputLogPath(out string filePath)
    {
        var smokeOutputPath = Environment.GetEnvironmentVariable(SmokeOutputLogPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(smokeOutputPath))
        {
            filePath = smokeOutputPath;
            return true;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text file (*.txt)|*.txt|Log file (*.log)|*.log|All files (*.*)|*.*",
            Title = "Save Designer Output Log",
            AddExtension = true,
            DefaultExt = ".txt",
            FileName = $"designer-output-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(GetOwnerWindow()) != true)
        {
            filePath = string.Empty;
            return false;
        }

        filePath = dialog.FileName;
        return true;
    }

    public bool TryRunSourceImageManagement(SourceImageManagementDialogRequest request, out SourceImageManagementDialogResult result)
    {
        var dialog = new SourceImageManagementDialog(request)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            result = new SourceImageManagementDialogResult(0, "No changes were applied.");
            return false;
        }

        result = dialog.Result;
        return true;
    }

    public bool TryOpenTextFileInDefaultEditor(string filePath, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Target file path is empty.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            errorMessage = $"File not found: {filePath}";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                Verb = "open"
            });

            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryOpenUriInDefaultBrowser(string uri, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            errorMessage = "Target URI is empty.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
                Verb = "open"
            });

            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryGetDefaultEchoMessagesRefreshTarget(bool projectTargetAvailable, out DefaultEchoMessagesRefreshTarget target)
    {
        var owner = GetOwnerWindow();
        var message = projectTargetAvailable
            ? "Choose refresh target:\nYes = Project defaults\nNo = Application defaults\nCancel = Do nothing"
            : "Project defaults are unavailable because no project is open.\nRefresh application defaults now?";
        var caption = "Refresh Default Echo Messages Skeleton";

        var result = owner is null
            ? System.Windows.MessageBox.Show(message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question)
            : System.Windows.MessageBox.Show(owner, message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            target = DefaultEchoMessagesRefreshTarget.Application;
            return false;
        }

        if (projectTargetAvailable && result == MessageBoxResult.Yes)
        {
            target = DefaultEchoMessagesRefreshTarget.Project;
            return true;
        }

        target = DefaultEchoMessagesRefreshTarget.Application;
        return result == MessageBoxResult.No || (!projectTargetAvailable && result == MessageBoxResult.Yes);
    }

    private static Window? GetOwnerWindow()
    {
        var activeWindow = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        return activeWindow ?? System.Windows.Application.Current?.MainWindow;
    }

    private static string ResolveStarterProjectsRoot(string? configuredOverride)
    {
        if (!string.IsNullOrWhiteSpace(configuredOverride))
        {
            return configuredOverride;
        }

        return Path.Combine(AppContext.BaseDirectory, "StarterProjects");
    }

    private static string ResolveDefaultProjectFolder(string? savedFolder)
    {
        if (!string.IsNullOrWhiteSpace(savedFolder) && Directory.Exists(savedFolder))
        {
            return savedFolder!;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static string ResolveDefaultStarterBrowseFolder(string? savedFolder, string starterProjectsRoot)
    {
        if (!string.IsNullOrWhiteSpace(savedFolder) && Directory.Exists(savedFolder))
        {
            return savedFolder!;
        }

        if (Directory.Exists(starterProjectsRoot))
        {
            return starterProjectsRoot;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static IReadOnlyList<StarterProjectOption> DiscoverStarterProjects(string starterProjectsRoot)
    {
        if (!Directory.Exists(starterProjectsRoot))
        {
            return Array.Empty<StarterProjectOption>();
        }

        var projectFiles = Directory.EnumerateFiles(starterProjectsRoot, "*.sbe.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var options = new List<StarterProjectOption>(projectFiles.Count);
        foreach (var projectFile in projectFiles)
        {
            var displayName = BuildStarterDisplayName(projectFile, starterProjectsRoot);
            options.Add(new StarterProjectOption
            {
                DisplayName = displayName,
                ProjectFilePath = projectFile
            });
        }

        return options;
    }

    private static string BuildStarterDisplayName(string projectFilePath, string starterProjectsRoot)
    {
        var projectName = TryReadProjectName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        }

        var relativePath = Path.GetRelativePath(starterProjectsRoot, projectFilePath);
        return $"{projectName} ({relativePath})";
    }

    private static string TryReadProjectName(string projectFilePath)
    {
        try
        {
            using var stream = File.OpenRead(projectFilePath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("name", out var nameProperty)
                && nameProperty.ValueKind == JsonValueKind.String)
            {
                return nameProperty.GetString()?.Trim() ?? string.Empty;
            }
        }
        catch
        {
        }

        return string.Empty;
    }
}
