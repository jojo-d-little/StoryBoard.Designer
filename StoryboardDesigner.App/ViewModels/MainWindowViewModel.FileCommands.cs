using System.Windows.Input;
using System.IO;
using System.Text.Json;
using Storyboard.Shared.Serialization;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Orchestration.Workflows;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.RuntimeContracts.Dtos;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;
using StoryboardDesigner.App.Validation.Rules.Objects;
using StoryboardDesigner.App.Validation.Rules.Project;
using StoryboardDesigner.App.Validation.Rules.Scripting;

namespace StoryboardDesigner.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private enum TreeValidationCompletionMode
    {
        FullReport,
        StopOnFirstBlocking
    }

    private RelayCommand _createNewProjectCommand = null!;
    private RelayCommand _openProjectCommand = null!;
    private RelayCommandOfT<string> _openRecentProjectCommand = null!;
    private RelayCommand _saveProjectCommand = null!;
    private RelayCommand _saveProjectAsCommand = null!;
    private RelayCommand _importGlobalsFromProjectCommand = null!;
    private RelayCommand _exportCleanProjectCommand = null!;
    private RelayCommand _validateProjectCommand = null!;
    private RelayCommand _manageSourceImagesCommand = null!;
    private RelayCommand _openSharedVariablesManagerToolCommand = null!;
    private RelayCommand _editApplicationDefaultEchoMessagesCommand = null!;
    private RelayCommand _editProjectDefaultEchoMessagesCommand = null!;
    private RelayCommand _refreshDefaultEchoMessagesSkeletonCommand = null!;
    private RelayCommand _editApplicationDefaultPresentationCuesCommand = null!;
    private RelayCommand _editProjectDefaultPresentationCuesCommand = null!;
    private RelayCommand _editApplicationPresentationCueCatalogCommand = null!;
    private RelayCommand _runSimulatorCommand = null!;
    private RelayCommand _runDevelopmentCommand = null!;
    private RelayCommand _developmentLaunchSetupCommand = null!;
    private RelayCommand _simulatorSetupCommand = null!;
    private RelayCommand _hideEmptyConfigurationGlobalCommand = null!;
    private RelayCommand _showEmptyConfigurationGlobalCommand = null!;

    private const string DefaultEchoMessagesFileName = "default echo messages.json";
    private const string RuntimeSelectionCueDefaultsFileName = "selection-cue-defaults.json";
    private const string PresentationCueCatalogRelativePath = "Config\\presentation-effects.catalog.json";

    public ICommand CreateNewProjectCommand => _createNewProjectCommand;
    public ICommand OpenProjectCommand => _openProjectCommand;
    public ICommand SaveProjectCommand => _saveProjectCommand;
    public ICommand SaveProjectAsCommand => _saveProjectAsCommand;
    public ICommand ImportGlobalsFromProjectCommand => _importGlobalsFromProjectCommand;
    public ICommand ExportCleanProjectCommand => _exportCleanProjectCommand;
    public ICommand ValidateProjectCommand => _validateProjectCommand;
    public ICommand OpenRecentProjectCommand => _openRecentProjectCommand;
    public ICommand ManageSourceImagesCommand => _manageSourceImagesCommand;
    public ICommand OpenSharedVariablesManagerToolCommand => _openSharedVariablesManagerToolCommand;
    public ICommand EditApplicationDefaultEchoMessagesCommand => _editApplicationDefaultEchoMessagesCommand;
    public ICommand EditProjectDefaultEchoMessagesCommand => _editProjectDefaultEchoMessagesCommand;
    public ICommand RefreshDefaultEchoMessagesSkeletonCommand => _refreshDefaultEchoMessagesSkeletonCommand;
    public ICommand EditApplicationDefaultPresentationCuesCommand => _editApplicationDefaultPresentationCuesCommand;
    public ICommand EditProjectDefaultPresentationCuesCommand => _editProjectDefaultPresentationCuesCommand;
    public ICommand EditApplicationPresentationCueCatalogCommand => _editApplicationPresentationCueCatalogCommand;
    public ICommand RunSimulatorCommand => _runSimulatorCommand;
    public ICommand RunDevelopmentCommand => _runDevelopmentCommand;
    public ICommand DevelopmentLaunchSetupCommand => _developmentLaunchSetupCommand;
    public ICommand SimulatorSetupCommand => _simulatorSetupCommand;
    public ICommand HideEmptyConfigurationGlobalCommand => _hideEmptyConfigurationGlobalCommand;
    public ICommand ShowEmptyConfigurationGlobalCommand => _showEmptyConfigurationGlobalCommand;

    private void InitializeFileCommands()
    {
        _createNewProjectCommand = new RelayCommand(CreateNewProjectExecute, CanExecuteGlobalFileWorkflowCommand);
        _openProjectCommand = new RelayCommand(OpenProjectExecute, CanExecuteGlobalFileWorkflowCommand);
        _openRecentProjectCommand = new RelayCommandOfT<string>(OpenRecentProjectExecute, path => !string.IsNullOrWhiteSpace(path) && CanExecuteGlobalFileWorkflowCommand());
        _saveProjectCommand = new RelayCommand(SaveProjectExecute, CanExecuteGlobalFileWorkflowCommand);
        _saveProjectAsCommand = new RelayCommand(SaveProjectAsExecute, CanExecuteGlobalFileWorkflowCommand);
        _importGlobalsFromProjectCommand = new RelayCommand(ImportGlobalsFromProjectExecute, CanExecuteGlobalFileWorkflowCommand);
        _exportCleanProjectCommand = new RelayCommand(ExportCleanProjectExecute, CanExecuteGlobalFileWorkflowCommand);
        _validateProjectCommand = new RelayCommand(ValidateProjectExecute, CanExecuteGlobalFileWorkflowCommand);
        _manageSourceImagesCommand = new RelayCommand(ManageSourceImagesExecute, CanExecuteGlobalFileWorkflowCommand);
        _openSharedVariablesManagerToolCommand = new RelayCommand(OpenSharedVariablesManagerToolExecute, CanExecuteGlobalFileWorkflowCommand);
        _editApplicationDefaultEchoMessagesCommand = new RelayCommand(EditApplicationDefaultEchoMessagesExecute, CanExecuteGlobalFileWorkflowCommand);
        _editProjectDefaultEchoMessagesCommand = new RelayCommand(EditProjectDefaultEchoMessagesExecute, CanExecuteEditProjectDefaultEchoMessages);
        _refreshDefaultEchoMessagesSkeletonCommand = new RelayCommand(RefreshDefaultEchoMessagesSkeletonExecute, CanExecuteGlobalFileWorkflowCommand);
        _editApplicationDefaultPresentationCuesCommand = new RelayCommand(EditApplicationDefaultPresentationCuesExecute, CanExecuteGlobalFileWorkflowCommand);
        _editProjectDefaultPresentationCuesCommand = new RelayCommand(EditProjectDefaultPresentationCuesExecute, CanExecuteEditProjectDefaultEchoMessages);
        _editApplicationPresentationCueCatalogCommand = new RelayCommand(EditApplicationPresentationCueCatalogExecute, CanExecuteGlobalFileWorkflowCommand);
        _runSimulatorCommand = new RelayCommand(RunSimulatorExecute, CanExecuteGlobalFileWorkflowCommand);
        _runDevelopmentCommand = new RelayCommand(RunDevelopmentExecute, CanExecuteDevelopmentLaunchCommand);
        _developmentLaunchSetupCommand = new RelayCommand(DevelopmentLaunchSetupExecute, CanExecuteGlobalFileWorkflowCommand);
        _simulatorSetupCommand = new RelayCommand(SimulatorSetupExecute, CanExecuteGlobalFileWorkflowCommand);
        _hideEmptyConfigurationGlobalCommand = new RelayCommand(HideEmptyConfigurationGlobalExecute, CanExecuteGlobalFileWorkflowCommand);
        _showEmptyConfigurationGlobalCommand = new RelayCommand(ShowEmptyConfigurationGlobalExecute, CanExecuteGlobalFileWorkflowCommand);
    }

    private bool CanExecuteGlobalFileWorkflowCommand()
    {
        return !_isShellWorkflowBusy;
    }

    private bool CanExecuteEditProjectDefaultEchoMessages()
    {
        return CanExecuteGlobalFileWorkflowCommand() && !string.IsNullOrWhiteSpace(_projectFilePath);
    }

    private void RefreshFileCommandCanExecuteStates()
    {
        _createNewProjectCommand.RaiseCanExecuteChanged();
        _openProjectCommand.RaiseCanExecuteChanged();
        _openRecentProjectCommand.RaiseCanExecuteChanged();
        _saveProjectCommand.RaiseCanExecuteChanged();
        _saveProjectAsCommand.RaiseCanExecuteChanged();
        _importGlobalsFromProjectCommand.RaiseCanExecuteChanged();
        _exportCleanProjectCommand.RaiseCanExecuteChanged();
        _validateProjectCommand.RaiseCanExecuteChanged();
        _manageSourceImagesCommand.RaiseCanExecuteChanged();
        _openSharedVariablesManagerToolCommand.RaiseCanExecuteChanged();
        _editApplicationDefaultEchoMessagesCommand.RaiseCanExecuteChanged();
        _editProjectDefaultEchoMessagesCommand.RaiseCanExecuteChanged();
        _refreshDefaultEchoMessagesSkeletonCommand.RaiseCanExecuteChanged();
        _editApplicationDefaultPresentationCuesCommand.RaiseCanExecuteChanged();
        _editProjectDefaultPresentationCuesCommand.RaiseCanExecuteChanged();
        _editApplicationPresentationCueCatalogCommand.RaiseCanExecuteChanged();
        _runSimulatorCommand.RaiseCanExecuteChanged();
        _runDevelopmentCommand.RaiseCanExecuteChanged();
        _developmentLaunchSetupCommand.RaiseCanExecuteChanged();
        _simulatorSetupCommand.RaiseCanExecuteChanged();
        _hideEmptyConfigurationGlobalCommand.RaiseCanExecuteChanged();
        _showEmptyConfigurationGlobalCommand.RaiseCanExecuteChanged();
    }

    private void HideEmptyConfigurationGlobalExecute()
    {
        ApplyHideEmptyConfigurationGlobally(hideEmptyConfiguration: true);
    }

    private void ShowEmptyConfigurationGlobalExecute()
    {
        ApplyHideEmptyConfigurationGlobally(hideEmptyConfiguration: false);
    }

    private void OpenSharedVariablesManagerToolExecute()
    {
        OpenSharedVariablesManager(selectedSharedVariableId: null);
    }

    private void EditApplicationDefaultEchoMessagesExecute()
    {
        var path = Path.Combine(AppContext.BaseDirectory, DefaultEchoMessagesFileName);
        OpenDefaultEchoMessagesFile(path, "Application Default Echo Messages");
    }

    private void EditProjectDefaultEchoMessagesExecute()
    {
        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            _projectUiService.ShowWarning("Open or save a project before editing project default echo messages.", "Edit Project Default Echo Messages");
            return;
        }

        var projectFolder = Path.GetDirectoryName(_projectFilePath);
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _projectUiService.ShowWarning("Unable to resolve the current project folder.", "Edit Project Default Echo Messages");
            return;
        }

        var path = Path.Combine(projectFolder, DefaultEchoMessagesFileName);
        OpenDefaultEchoMessagesFile(path, "Project Default Echo Messages");
    }

    private void OpenDefaultEchoMessagesFile(string filePath, string label)
    {
        try
        {
            EnsureDefaultEchoMessagesFileExists(filePath);
        }
        catch (Exception ex)
        {
            _projectUiService.ShowWarning($"Unable to prepare {label} file.{Environment.NewLine}{ex.Message}", label);
            return;
        }

        if (!_projectUiService.TryOpenTextFileInDefaultEditor(filePath, out var errorMessage))
        {
            var detail = string.IsNullOrWhiteSpace(errorMessage) ? "Unable to open file." : errorMessage;
            _projectUiService.ShowWarning($"Unable to open {label}.{Environment.NewLine}{detail}", label);
            return;
        }

        ExportStatus = $"Opened {label}: {filePath}";
    }

    private void RefreshDefaultEchoMessagesSkeletonExecute()
    {
        var projectTargetAvailable = !string.IsNullOrWhiteSpace(_projectFilePath);
        if (!_projectUiService.TryGetDefaultEchoMessagesRefreshTarget(projectTargetAvailable, out var target))
        {
            return;
        }

        var filePath = ResolveDefaultEchoMessagesPath(target);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _projectUiService.ShowWarning("Unable to resolve defaults file path for selected target.", "Refresh Default Echo Messages Skeleton");
            return;
        }

        try
        {
            EnsureDefaultEchoMessagesFileExists(filePath);
            var summary = RefreshDefaultEchoMessagesFile(filePath);
            var targetLabel = target == DefaultEchoMessagesRefreshTarget.Project ? "project" : "application";
            var message = $"Refreshed {targetLabel} defaults: {summary.AddedCount} added, {summary.RemovedCount} removed.";
            ExportStatus = message;
            _projectUiService.ShowInformation(message, "Refresh Default Echo Messages Skeleton");
        }
        catch (Exception ex)
        {
            _projectUiService.ShowWarning($"Unable to refresh defaults file.{Environment.NewLine}{ex.Message}", "Refresh Default Echo Messages Skeleton");
        }
    }

    private void EditApplicationDefaultPresentationCuesExecute()
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appDataFolder))
        {
            _projectUiService.ShowWarning("Unable to resolve application data folder for runtime default cue settings.", "Edit Application Default Presentation Cues");
            return;
        }

        var path = Path.Combine(appDataFolder, "StoryboardRuntime", RuntimeSelectionCueDefaultsFileName);
        OpenJsonConfigFile(path, "Application Default Presentation Cues", EnsureRuntimeSelectionCueDefaultsFileExists);
    }

    private void EditApplicationPresentationCueCatalogExecute()
    {
        var path = Path.Combine(AppContext.BaseDirectory, PresentationCueCatalogRelativePath);
        OpenJsonConfigFile(path, "Application Presentation Cue Catalog", EnsurePresentationCueCatalogFileExists);
    }

    private void EditProjectDefaultPresentationCuesExecute()
    {
        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            _projectUiService.ShowWarning("Open or save a project before editing project default presentation cues.", "Edit Project Default Presentation Cues");
            return;
        }

        if (!_projectUiService.TryOpenTextFileInDefaultEditor(_projectFilePath, out var errorMessage))
        {
            var detail = string.IsNullOrWhiteSpace(errorMessage) ? "Unable to open file." : errorMessage;
            _projectUiService.ShowWarning($"Unable to open Project JSON.{Environment.NewLine}{detail}", "Edit Project Default Presentation Cues");
            return;
        }

        ExportStatus = $"Opened Project JSON for default presentation cue overrides: {_projectFilePath}";
    }

    private void OpenJsonConfigFile(string filePath, string label, Action<string> ensureExists)
    {
        try
        {
            ensureExists(filePath);
        }
        catch (Exception ex)
        {
            _projectUiService.ShowWarning($"Unable to prepare {label} file.{Environment.NewLine}{ex.Message}", label);
            return;
        }

        if (!_projectUiService.TryOpenTextFileInDefaultEditor(filePath, out var errorMessage))
        {
            var detail = string.IsNullOrWhiteSpace(errorMessage) ? "Unable to open file." : errorMessage;
            _projectUiService.ShowWarning($"Unable to open {label}.{Environment.NewLine}{detail}", label);
            return;
        }

        ExportStatus = $"Opened {label}: {filePath}";
    }

    private string ResolveDefaultEchoMessagesPath(DefaultEchoMessagesRefreshTarget target)
    {
        if (target == DefaultEchoMessagesRefreshTarget.Application)
        {
            return Path.Combine(AppContext.BaseDirectory, DefaultEchoMessagesFileName);
        }

        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            return string.Empty;
        }

        var projectFolder = Path.GetDirectoryName(_projectFilePath);
        return string.IsNullOrWhiteSpace(projectFolder)
            ? string.Empty
            : Path.Combine(projectFolder, DefaultEchoMessagesFileName);
    }

    private static EchoDefaultsRefreshSummary RefreshDefaultEchoMessagesFile(string filePath)
    {
        var desired = BuildDefaultEchoMessagesSkeleton();
        var options = StoryboardJsonSerializerOptions.Create();

        Dictionary<string, Dictionary<string, string>> current;
        var raw = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            current = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        }
        else
        {
            current = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(raw)
                ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        }

        var desiredActionKeys = new HashSet<string>(desired.Keys, StringComparer.OrdinalIgnoreCase);
        var currentActionKeys = current.Keys.ToList();
        var removed = 0;
        var added = 0;

        foreach (var actionKey in currentActionKeys)
        {
            if (desiredActionKeys.Contains(actionKey))
            {
                continue;
            }

            removed++;
            current.Remove(actionKey);
        }

        foreach (var (actionKey, desiredTokensReadonly) in desired)
        {
            if (!current.TryGetValue(actionKey, out var currentTokens))
            {
                added++;
                current[actionKey] = new Dictionary<string, string>(desiredTokensReadonly, StringComparer.Ordinal);
                continue;
            }

            var desiredTokens = new Dictionary<string, string>(desiredTokensReadonly, StringComparer.Ordinal);
            var desiredTokenKeys = new HashSet<string>(desiredTokens.Keys, StringComparer.OrdinalIgnoreCase);
            var currentTokenKeys = currentTokens.Keys.ToList();

            foreach (var tokenKey in currentTokenKeys)
            {
                if (desiredTokenKeys.Contains(tokenKey))
                {
                    continue;
                }

                removed++;
                currentTokens.Remove(tokenKey);
            }

            foreach (var desiredToken in desiredTokens.Keys)
            {
                if (currentTokens.ContainsKey(desiredToken))
                {
                    continue;
                }

                added++;
                currentTokens[desiredToken] = string.Empty;
            }
        }

        var normalized = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (actionKey, _) in desired)
        {
            if (!current.TryGetValue(actionKey, out var tokenMap))
            {
                continue;
            }

            var orderedTokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in desired[actionKey].Keys)
            {
                orderedTokens[token] = tokenMap.TryGetValue(token, out var value) ? value ?? string.Empty : string.Empty;
            }

            normalized[actionKey] = orderedTokens;
        }

        var json = JsonSerializer.Serialize(normalized, options);
        File.WriteAllText(filePath, json);
        return new EchoDefaultsRefreshSummary(added, removed);
    }

    private static void EnsureRuntimeSelectionCueDefaultsFileExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var defaults = new RuntimeSelectionCueDefaultsFile
        {
            PrimarySelectionCueEffectKey = GameCommandPresentationEffectKeyCatalog.GetDefaultSelectionOutlineEffectKey(),
            SecondarySelectionCueEffectKey = GameCommandPresentationEffectKeyCatalog.GetDefaultSecondarySelectionOutlineEffectKey()
        };
        var options = StoryboardJsonSerializerOptions.Create();
        var json = JsonSerializer.Serialize(defaults, options);
        File.WriteAllText(filePath, json);
    }

    private static void EnsurePresentationCueCatalogFileExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var defaults = GameCommandPresentationEffectsCatalogLoader.BuildDefault();
        var options = StoryboardJsonSerializerOptions.Create();
        var json = JsonSerializer.Serialize(defaults, options);
        File.WriteAllText(filePath, json);
    }

    private sealed record EchoDefaultsRefreshSummary(int AddedCount, int RemovedCount);

    private sealed class RuntimeSelectionCueDefaultsFile
    {
        public string PrimarySelectionCueEffectKey { get; init; } = GameCommandPresentationEffectKeyCatalog.GetDefaultSelectionOutlineEffectKey();
        public string SecondarySelectionCueEffectKey { get; init; } = GameCommandPresentationEffectKeyCatalog.GetDefaultSecondarySelectionOutlineEffectKey();
    }

    private static void EnsureDefaultEchoMessagesFileExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var defaults = BuildDefaultEchoMessagesSkeleton();
        var options = StoryboardJsonSerializerOptions.Create();
        var json = JsonSerializer.Serialize(defaults, options);
        File.WriteAllText(filePath, json);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildDefaultEchoMessagesSkeleton()
    {
        var byAction = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var actionType in CommandActionTypeValues.All)
        {
            var tokens = RuntimeActionResultCodeRegistry
                .GetSupportedResultCodes(actionType)
                .Select(descriptor => descriptor.Token)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tokenDefaults = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in tokens)
            {
                tokenDefaults[token] = string.Empty;
            }

            byAction[actionType.ToString()] = tokenDefaults;
        }

        return byAction;
    }

    private void RunSimulatorExecute()
    {
        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            _projectUiService.ShowWarning("Save the project before running Simulator.", "Run Simulator");
            return;
        }

        if (!SaveProjectWithValidationSummary("Run Simulator"))
        {
            _projectUiService.ShowWarning(ExportStatus, "Run Simulator");
            return;
        }

        if (!TryEnsureNoBlockingValidationErrors("run simulator", out var blockingValidationFailure))
        {
            ExportStatus = blockingValidationFailure;
            _projectUiService.ShowWarning(blockingValidationFailure, "Run Simulator");
            return;
        }

        var request = new RunExternalSimulatorRequest
        {
            ProjectFilePath = _projectFilePath,
            ReplayFilePath = string.IsNullOrWhiteSpace(_project.SimulatorReplayFilePath) ? null : _project.SimulatorReplayFilePath,
            ReplaySpeed = _project.SimulatorReplaySpeed
        };

        var result = _externalSimulatorWorkflowService.RunSimulator(request);
        if (!result.Success)
        {
            var details = result.Diagnostics.Count == 0
                ? result.StatusMessage
                : $"{result.StatusMessage}{Environment.NewLine}{string.Join(Environment.NewLine, result.Diagnostics)}";
            _projectUiService.ShowWarning(details, "Run Simulator");
        }

        ExportStatus = result.StatusMessage;
    }

    private bool CanExecuteDevelopmentLaunchCommand()
    {
        return CanExecuteGlobalFileWorkflowCommand() && !_isDevelopmentLaunchBusy;
    }

    private async void RunDevelopmentExecute()
    {
        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            _projectUiService.ShowWarning("Save the project before launching WebPortal.", "Run Development WebPortal");
            return;
        }

        if (!SaveProjectWithValidationSummary("Run Development WebPortal"))
        {
            _projectUiService.ShowWarning(ExportStatus, "Run Development WebPortal");
            return;
        }

        if (!TryEnsureNoBlockingValidationErrors("launch development WebPortal", out var blockingValidationFailure))
        {
            ExportStatus = blockingValidationFailure;
            _projectUiService.ShowWarning(blockingValidationFailure, "Run Development WebPortal");
            return;
        }

        var runtimeProjectPath = ResolveRuntimeProjectPath(_projectFilePath);
        _isDevelopmentLaunchBusy = true;
        RefreshFileCommandCanExecuteStates();

        try
        {
            var result = await _developmentGameHostWorkflowService.LaunchAsync(
                new DevelopmentGameHostLaunchRequest
                {
                    ProjectFilePath = _projectFilePath,
                    RuntimeProjectPath = runtimeProjectPath
                });

            foreach (var launchDiagnostic in result.LaunchDiagnostics)
            {
                AppendOutputConsoleLine(launchDiagnostic);
            }

            if (!result.Success)
            {
                var details = result.Diagnostics.Count == 0
                    ? result.StatusMessage
                    : $"{result.StatusMessage}{Environment.NewLine}{string.Join(Environment.NewLine, result.Diagnostics)}";
                _projectUiService.ShowWarning(details, "Run Development WebPortal");
                ExportStatus = result.StatusMessage;
                return;
            }

            var browserError = string.Empty;
            var browserOpened = result.BrowserUri is not null
                && _projectUiService.TryOpenUriInDefaultBrowser(result.BrowserUri.AbsoluteUri, out browserError);
            if (!browserOpened)
            {
                _developmentGameHostWorkflowService.Stop();
                var detail = string.IsNullOrWhiteSpace(browserError) ? "Unable to open the system browser." : browserError;
                _projectUiService.ShowWarning($"Development host is ready, but WebPortal could not be opened.{Environment.NewLine}{detail}", "Run Development WebPortal");
                ExportStatus = "Development host ready, but browser launch failed.";
                return;
            }

            ExportStatus = $"Development WebPortal launched at {result.BrowserUri} ({result.GameKey}).";
        }
        catch (Exception ex)
        {
            _developmentGameHostWorkflowService.Stop();
            _projectUiService.ShowWarning($"Development launch failed.{Environment.NewLine}{ex.Message}", "Run Development WebPortal");
            ExportStatus = "Development launch failed.";
        }
        finally
        {
            _isDevelopmentLaunchBusy = false;
            RefreshFileCommandCanExecuteStates();
        }
    }

    private static string ResolveRuntimeProjectPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        if (baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^4];
        }

        return Path.Combine(folder, "GameRuntimeJson", $"{baseName}.sbr.runtime.json");
    }

    private void SimulatorSetupExecute()
    {
        var request = new SimulatorSetupDialogRequest
        {
            ProjectFilePath = _projectFilePath,
            ReplayFilePath = _project.SimulatorReplayFilePath,
            ReplaySpeed = _project.SimulatorReplaySpeed
        };

        var result = _externalSimulatorWorkflowService.OpenSimulatorSetup(request);

        if (result.AppliedChanges)
        {
            var replayFileChanged = !string.Equals(_project.SimulatorReplayFilePath, result.ReplayFilePath, StringComparison.Ordinal);
            var replaySpeedChanged = _project.SimulatorReplaySpeed != result.ReplaySpeed;
            if (replayFileChanged || replaySpeedChanged)
            {
                _project.SimulatorReplayFilePath = result.ReplayFilePath;
                _project.SimulatorReplaySpeed = result.ReplaySpeed;
                NotifyProjectEdited();
            }
        }

        ExportStatus = result.StatusMessage;
    }

    private void DevelopmentLaunchSetupExecute()
    {
        var preferences = _projectCreationPreferencesService.Load();
        var dialog = new Views.DevelopmentLaunchSetupDialog(
            preferences.DevelopmentUsername,
            preferences.GameHostExecutablePath,
            preferences.WebPortalRootPath)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            ExportStatus = "Development WebPortal setup canceled.";
            return;
        }

        preferences.DevelopmentUsername = dialog.DevelopmentUsername;
        preferences.GameHostExecutablePath = dialog.GameHostExecutablePath;
        preferences.WebPortalRootPath = dialog.WebPortalRootPath;
        _projectCreationPreferencesService.Save(preferences);
        ExportStatus = "Development WebPortal setup saved.";
    }

    private void ManageSourceImagesExecute()
    {
        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            _projectUiService.ShowWarning("Save the project before running Source Image Management.", "Source Image Management");
            return;
        }

        var request = new SourceImageManagementDialogRequest(_project, _projectFilePath);
        if (!_projectUiService.TryRunSourceImageManagement(request, out var result))
        {
            return;
        }

        if (result.ChangesApplied > 0)
        {
            NotifyProjectEdited();
        }

        ExportStatus = result.Summary;
        _projectUiService.ShowInformation(result.Summary, "Source Image Management");
    }

    private void ImportGlobalsFromProjectExecute()
    {
        if (!_projectUiService.TryGetImportGlobalNodeProjectPath(out var globalsFilePath))
        {
            return;
        }

        var sourceProject = _jsonExportService.TryLoadGlobalNodeForImport(globalsFilePath);
        if (sourceProject is null)
        {
            _projectUiService.ShowWarning("Unable to read globals from the selected file.", "Import Globals");
            return;
        }

        var request = new GlobalImportSelectionRequest
        {
            SourceProjectPath = globalsFilePath,
            VerbCount = sourceProject.CommandVerbs.Count,
            DirectionalCount = sourceProject.Directionals.Count,
            GlobalActionCount = sourceProject.GlobalAvailableActions.Count,
            GlobalSoundEffectCount = sourceProject.GlobalSoundEffectLibraryEntries.Count,
            GlobalEventSubscriptionCount = sourceProject.GlobalEventSubscriptions.Count,
            GlobalTimerCount = sourceProject.GlobalTimerDefinitions.Count,
            TemplateObjectCount = sourceProject.ObjectTemplates.Count,
            RoomTemplateCount = sourceProject.RoomTemplates.Count,
            BaseObjectCount = sourceProject.BaseObjects.Count,
            GlobalObjectCount = sourceProject.GlobalObjects.Count
        };

        if (!_projectUiService.TryGetImportGlobalNodeSelection(request, out var selection))
        {
            return;
        }

        var result = ApplyGlobalImportSelection(sourceProject, selection);
        if (result.ChangedCount <= 0)
        {
            ExportStatus = "Globals import completed with no changes.";
            return;
        }

        SyncProjectCommandCatalogsFromModel();
        NotifyProjectEdited();
        ExportStatus = $"Imported globals: +{result.AddedCount} added, ~{result.ReplacedCount} replaced, {result.SkippedCount} skipped.";
    }

    private ImportGlobalsResult ApplyGlobalImportSelection(
        ProjectGlobalNodeImportData sourceProject,
        GlobalImportSelectionResult selection)
    {
        var result = new ImportGlobalsResult();

        if (selection.ImportVerbs)
        {
            var verbResult = ImportTokenCollection(_project.CommandVerbs, sourceProject.CommandVerbs, selection.CollisionStrategy);
            result.VerbsAdded += verbResult.Added;
            result.VerbsSkipped += verbResult.Skipped;
            result.AddedCount += verbResult.Added;
            result.SkippedCount += verbResult.Skipped;
        }

        if (selection.ImportDirectionals)
        {
            var directionalResult = ImportTokenCollection(_project.Directionals, sourceProject.Directionals, selection.CollisionStrategy);
            result.DirectionalsAdded += directionalResult.Added;
            result.DirectionalsSkipped += directionalResult.Skipped;
            result.AddedCount += directionalResult.Added;
            result.SkippedCount += directionalResult.Skipped;

            var mappingResult = ImportDirectionalMappings(_project.DirectionalTraversalMappings, sourceProject.DirectionalTraversalMappings, selection.CollisionStrategy);
            result.DirectionalMappingsAdded += mappingResult.Added;
            result.DirectionalMappingsReplaced += mappingResult.Replaced;
            result.DirectionalMappingsSkipped += mappingResult.Skipped;
            result.AddedCount += mappingResult.Added;
            result.ReplacedCount += mappingResult.Replaced;
            result.SkippedCount += mappingResult.Skipped;
        }

        if (selection.ImportGlobalActions)
        {
            var globalActionResult = ImportActionCollection(
                _project.GlobalScope.AvailableActions,
                sourceProject.GlobalAvailableActions,
                selection.CollisionStrategy);
            result.GlobalActionsAdded += globalActionResult.Added;
            result.GlobalActionsReplaced += globalActionResult.Replaced;
            result.GlobalActionsSkipped += globalActionResult.Skipped;
            result.AddedCount += globalActionResult.Added;
            result.ReplacedCount += globalActionResult.Replaced;
            result.SkippedCount += globalActionResult.Skipped;
        }

        if (selection.ImportGlobalSoundEffects)
        {
            var soundEffectResult = ImportSoundEffectCollection(
                _project.GlobalScope.SoundEffectLibraryEntries,
                sourceProject.GlobalSoundEffectLibraryEntries,
                selection.CollisionStrategy);
            result.GlobalSoundEffectsAdded += soundEffectResult.Added;
            result.GlobalSoundEffectsReplaced += soundEffectResult.Replaced;
            result.GlobalSoundEffectsSkipped += soundEffectResult.Skipped;
            result.AddedCount += soundEffectResult.Added;
            result.ReplacedCount += soundEffectResult.Replaced;
            result.SkippedCount += soundEffectResult.Skipped;
        }

        if (selection.ImportGlobalEventSubscriptions)
        {
            var eventResult = ImportEventSubscriptionCollection(
                _project.GlobalScope.EventSubscriptions,
                sourceProject.GlobalEventSubscriptions,
                selection.CollisionStrategy);
            result.GlobalEventSubscriptionsAdded += eventResult.Added;
            result.GlobalEventSubscriptionsReplaced += eventResult.Replaced;
            result.GlobalEventSubscriptionsSkipped += eventResult.Skipped;
            result.AddedCount += eventResult.Added;
            result.ReplacedCount += eventResult.Replaced;
            result.SkippedCount += eventResult.Skipped;
        }

        if (selection.ImportGlobalTimers)
        {
            var timerResult = ImportTimerDefinitionCollection(
                _project.GlobalScope.TimerDefinitions,
                sourceProject.GlobalTimerDefinitions,
                selection.CollisionStrategy);
            result.GlobalTimersAdded += timerResult.Added;
            result.GlobalTimersReplaced += timerResult.Replaced;
            result.GlobalTimersSkipped += timerResult.Skipped;
            result.AddedCount += timerResult.Added;
            result.ReplacedCount += timerResult.Replaced;
            result.SkippedCount += timerResult.Skipped;
        }

        if (selection.ImportTemplateObjects)
        {
            var templateResult = ImportGameObjectCollection(
                _project.ObjectTemplates,
                sourceProject.ObjectTemplates,
                selection.CollisionStrategy);
            result.TemplateObjectsAdded += templateResult.Added;
            result.TemplateObjectsReplaced += templateResult.Replaced;
            result.TemplateObjectsSkipped += templateResult.Skipped;
            result.AddedCount += templateResult.Added;
            result.ReplacedCount += templateResult.Replaced;
            result.SkippedCount += templateResult.Skipped;
        }

        if (selection.ImportRoomTemplates)
        {
            var roomTemplateResult = ImportRoomCollection(
                _project.RoomTemplates,
                sourceProject.RoomTemplates,
                selection.CollisionStrategy);
            result.RoomTemplatesAdded += roomTemplateResult.Added;
            result.RoomTemplatesReplaced += roomTemplateResult.Replaced;
            result.RoomTemplatesSkipped += roomTemplateResult.Skipped;
            result.AddedCount += roomTemplateResult.Added;
            result.ReplacedCount += roomTemplateResult.Replaced;
            result.SkippedCount += roomTemplateResult.Skipped;
        }

        if (selection.ImportGlobalObjects)
        {
            var globalResult = ImportGameObjectCollection(
                _project.GlobalScope.GameObjects,
                sourceProject.GlobalObjects,
                selection.CollisionStrategy);
            result.GlobalObjectsAdded += globalResult.Added;
            result.GlobalObjectsReplaced += globalResult.Replaced;
            result.GlobalObjectsSkipped += globalResult.Skipped;
            result.AddedCount += globalResult.Added;
            result.ReplacedCount += globalResult.Replaced;
            result.SkippedCount += globalResult.Skipped;
        }

        if (selection.ImportBaseObjects)
        {
            var baseObjectResult = ImportGameObjectCollection(
                _project.BaseObjects,
                sourceProject.BaseObjects,
                selection.CollisionStrategy);
            result.BaseObjectsAdded += baseObjectResult.Added;
            result.BaseObjectsReplaced += baseObjectResult.Replaced;
            result.BaseObjectsSkipped += baseObjectResult.Skipped;
            result.AddedCount += baseObjectResult.Added;
            result.ReplacedCount += baseObjectResult.Replaced;
            result.SkippedCount += baseObjectResult.Skipped;
        }

        return result;
    }

    private static SectionImportResult ImportTokenCollection(
        IList<string> destination,
        IEnumerable<string> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();

        foreach (var entry in source)
        {
            var candidate = entry?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var exists = destination.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                result.Skipped++;
                continue;
            }

            destination.Add(candidate);
            result.Added++;
        }

        return result;
    }

    private static SectionImportResult ImportDirectionalMappings(
        IList<DirectionalTraversalMapping> destination,
        IEnumerable<DirectionalTraversalMapping> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();

        foreach (var mapping in source)
        {
            var token = mapping.Token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var existingIndex = destination
                .Select((entry, index) => new { entry, index })
                .FirstOrDefault(entry => string.Equals(entry.entry.Token, token, StringComparison.OrdinalIgnoreCase))
                ?.index;

            if (!existingIndex.HasValue)
            {
                destination.Add(new DirectionalTraversalMapping
                {
                    Token = token,
                    TraversalDirection = mapping.TraversalDirection
                });
                result.Added++;
                continue;
            }

            if (strategy == GlobalImportCollisionStrategy.KeepExisting)
            {
                result.Skipped++;
                continue;
            }

            destination[existingIndex.Value] = new DirectionalTraversalMapping
            {
                Token = token,
                TraversalDirection = mapping.TraversalDirection
            };
            result.Replaced++;
        }

        return result;
    }

    private static SectionImportResult ImportSoundEffectCollection(
        IList<SoundEffectLibraryEntry> destination,
        IEnumerable<SoundEffectLibraryEntry> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();

        foreach (var sourceEntry in source)
        {
            var key = sourceEntry.SoundEffectKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var existingIndex = destination
                .Select((entry, index) => new { entry, index })
                .FirstOrDefault(entry => string.Equals(entry.entry.SoundEffectKey?.Trim(), key, StringComparison.OrdinalIgnoreCase))
                ?.index;

            var clone = CloneSoundEffectLibraryEntry(sourceEntry);
            clone.SoundEffectKey = key;

            if (existingIndex is null)
            {
                destination.Add(clone);
                result.Added++;
                continue;
            }

            if (strategy == GlobalImportCollisionStrategy.KeepExisting)
            {
                result.Skipped++;
                continue;
            }

            var existing = destination[existingIndex.Value];
            clone.SoundEffectId = existing.SoundEffectId;
            destination[existingIndex.Value] = clone;
            result.Replaced++;
        }

        return result;
    }

    private static SectionImportResult ImportEventSubscriptionCollection(
        IList<EventSubscriptionDefinition> destination,
        IEnumerable<EventSubscriptionDefinition> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();

        foreach (var sourceEntry in source)
        {
            var entryKey = ResolveEventSubscriptionImportKey(sourceEntry);
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                continue;
            }

            var existingIndex = destination
                .Select((entry, index) => new { entry, index })
                .FirstOrDefault(entry => string.Equals(ResolveEventSubscriptionImportKey(entry.entry), entryKey, StringComparison.OrdinalIgnoreCase))
                ?.index;

            var clone = CloneEventSubscription(sourceEntry);
            clone.SubscriptionName = clone.SubscriptionName?.Trim() ?? string.Empty;
            clone.EventKey = clone.EventKey?.Trim() ?? string.Empty;

            if (existingIndex is null)
            {
                destination.Add(clone);
                result.Added++;
                continue;
            }

            if (strategy == GlobalImportCollisionStrategy.KeepExisting)
            {
                result.Skipped++;
                continue;
            }

            var existing = destination[existingIndex.Value];
            clone.Id = existing.Id;
            destination[existingIndex.Value] = clone;
            result.Replaced++;
        }

        return result;
    }

    private static SectionImportResult ImportTimerDefinitionCollection(
        IList<RuntimeTimerDefinitionDto> destination,
        IEnumerable<RuntimeTimerDefinitionDto> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();

        foreach (var sourceEntry in source)
        {
            var timerKey = sourceEntry.TimerKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(timerKey))
            {
                continue;
            }

            var existingIndex = destination
                .Select((entry, index) => new { entry, index })
                .FirstOrDefault(entry => string.Equals(entry.entry.TimerKey?.Trim(), timerKey, StringComparison.OrdinalIgnoreCase))
                ?.index;

            var clone = CloneTimerDefinition(sourceEntry);
            clone.TimerKey = timerKey;

            if (existingIndex is null)
            {
                destination.Add(clone);
                result.Added++;
                continue;
            }

            if (strategy == GlobalImportCollisionStrategy.KeepExisting)
            {
                result.Skipped++;
                continue;
            }

            destination[existingIndex.Value] = clone;
            result.Replaced++;
        }

        return result;
    }

    private static string ResolveEventSubscriptionImportKey(EventSubscriptionDefinition entry)
    {
        var subscriptionName = entry.SubscriptionName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(subscriptionName))
        {
            return subscriptionName;
        }

        return entry.EventKey?.Trim() ?? string.Empty;
    }

    private static SectionImportResult ImportActionCollection(
        IList<CommandAction> destination,
        IEnumerable<CommandAction> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();
        var sourceClones = CloneActions(source);

        foreach (var sourceAction in sourceClones)
        {
            var existingIndex = destination
                .Select((item, index) => new { item, index })
                .FirstOrDefault(entry => string.Equals(entry.item.Name, sourceAction.Name, StringComparison.OrdinalIgnoreCase))
                ?.index;

            if (existingIndex is null)
            {
                destination.Add(sourceAction);
                result.Added++;
                continue;
            }

            if (strategy == GlobalImportCollisionStrategy.KeepExisting)
            {
                result.Skipped++;
                continue;
            }

            var existing = destination[existingIndex.Value];
            sourceAction.Id = existing.Id;
            destination[existingIndex.Value] = sourceAction;
            result.Replaced++;
        }

        return result;
    }

    private static SectionImportResult ImportGameObjectCollection(
        IList<GameObject> destination,
        IEnumerable<GameObject> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();

        foreach (var sourceObject in source)
        {
            var existingIndex = destination
                .Select((item, index) => new { item, index })
                .FirstOrDefault(entry => string.Equals(entry.item.Name, sourceObject.Name, StringComparison.OrdinalIgnoreCase))
                ?.index;

            var clone = CloneTemplateObject(sourceObject);
            if (existingIndex is null)
            {
                destination.Add(clone);
                result.Added++;
                continue;
            }

            if (strategy == GlobalImportCollisionStrategy.KeepExisting)
            {
                result.Skipped++;
                continue;
            }

            var existing = destination[existingIndex.Value];
            clone.ObjectId = existing.ObjectId;
            destination[existingIndex.Value] = clone;
            result.Replaced++;
        }

        return result;
    }

    private static SectionImportResult ImportRoomCollection(
        IList<Room> destination,
        IEnumerable<Room> source,
        GlobalImportCollisionStrategy strategy)
    {
        var result = new SectionImportResult();

        foreach (var sourceRoom in source)
        {
            var existingIndex = destination
                .Select((item, index) => new { item, index })
                .FirstOrDefault(entry => string.Equals(entry.item.Name, sourceRoom.Name, StringComparison.OrdinalIgnoreCase))
                ?.index;

            var clone = CloneTemplateRoom(sourceRoom);
            if (existingIndex is null)
            {
                destination.Add(clone);
                result.Added++;
                continue;
            }

            if (strategy == GlobalImportCollisionStrategy.KeepExisting)
            {
                result.Skipped++;
                continue;
            }

            var existing = destination[existingIndex.Value];
            clone.Id = existing.Id;
            destination[existingIndex.Value] = clone;
            result.Replaced++;
        }

        return result;
    }

    private static Room CloneTemplateRoom(Room source)
    {
        var clone = new Room
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            NameInGame = source.NameInGame,
            Description = source.Description,
            ProducerNotes = source.ProducerNotes,
            RoomImageCanvasWidth = source.RoomImageCanvasWidth,
            RoomImageCanvasHeight = source.RoomImageCanvasHeight,
            RoomDisplayMode = source.RoomDisplayMode,
            ValidationErrors = source.ValidationErrors.ToList(),
            TraversalModeOverride = source.TraversalModeOverride,
            Commands = source.Commands.ToList(),
            AvailableActions = new System.Collections.ObjectModel.ObservableCollection<CommandAction>(CloneActions(source.AvailableActions)),
            Variables = source.Variables.Select(variable => new GamePropertyDefinition
            {
                Id = Guid.NewGuid(),
                Name = variable.Name,
                DefaultValue = variable.DefaultValue,
                Lifetime = variable.Lifetime,
                ValueRestriction = variable.ValueRestriction,
                SharedVariableId = variable.SharedVariableId
            }).ToList(),
            AdditionalVerbs = source.AdditionalVerbs.ToList(),
            AdditionalDirectionals = source.AdditionalDirectionals.ToList(),
            IgnoredValidationRuleIds = source.IgnoredValidationRuleIds.ToList(),
            EventSubscriptions = CloneEventSubscriptions(source.EventSubscriptions),
            TimerDefinitions = CloneTimerDefinitions(source.TimerDefinitions),
            AdditionalDirectionalTraversalMappings = source.AdditionalDirectionalTraversalMappings
                .Select(mapping => new DirectionalTraversalMapping
                {
                    Token = mapping.Token,
                    TraversalDirection = mapping.TraversalDirection
                })
                .ToList(),
            SoundEffectLibraryEntries = source.SoundEffectLibraryEntries
                .Select(CloneSoundEffectLibraryEntry)
                .ToList(),
            GameObjects = source.GameObjects.Select(CloneTemplateObject).ToList(),
            Images = source.Images.Select(image => new RoomImageEntry
            {
                Slot = image.Slot,
                OverlayRenderOrder = image.OverlayRenderOrder,
                OverlayOffsetX = image.OverlayOffsetX,
                OverlayOffsetY = image.OverlayOffsetY,
                OverlayRotationDegrees = image.OverlayRotationDegrees,
                Image = new RoomImageVariant
                {
                    FullImagePath = image.Image.FullImagePath,
                    GrayMapImagePath = image.Image.GrayMapImagePath,
                    NormalMapImagePath = image.Image.NormalMapImagePath
                }
            }).ToList()
        };

        return clone;
    }

    private sealed class SectionImportResult
    {
        public int Added { get; set; }
        public int Replaced { get; set; }
        public int Skipped { get; set; }
    }

    private sealed class ImportGlobalsResult
    {
        public int VerbsAdded { get; set; }
        public int VerbsSkipped { get; set; }
        public int DirectionalsAdded { get; set; }
        public int DirectionalsSkipped { get; set; }
        public int GlobalActionsAdded { get; set; }
        public int GlobalActionsReplaced { get; set; }
        public int GlobalActionsSkipped { get; set; }
        public int GlobalSoundEffectsAdded { get; set; }
        public int GlobalSoundEffectsReplaced { get; set; }
        public int GlobalSoundEffectsSkipped { get; set; }
        public int GlobalEventSubscriptionsAdded { get; set; }
        public int GlobalEventSubscriptionsReplaced { get; set; }
        public int GlobalEventSubscriptionsSkipped { get; set; }
        public int GlobalTimersAdded { get; set; }
        public int GlobalTimersReplaced { get; set; }
        public int GlobalTimersSkipped { get; set; }
        public int DirectionalMappingsAdded { get; set; }
        public int DirectionalMappingsReplaced { get; set; }
        public int DirectionalMappingsSkipped { get; set; }
        public int TemplateObjectsAdded { get; set; }
        public int TemplateObjectsReplaced { get; set; }
        public int TemplateObjectsSkipped { get; set; }
        public int RoomTemplatesAdded { get; set; }
        public int RoomTemplatesReplaced { get; set; }
        public int RoomTemplatesSkipped { get; set; }
        public int BaseObjectsAdded { get; set; }
        public int BaseObjectsReplaced { get; set; }
        public int BaseObjectsSkipped { get; set; }
        public int GlobalObjectsAdded { get; set; }
        public int GlobalObjectsReplaced { get; set; }
        public int GlobalObjectsSkipped { get; set; }
        public int AddedCount { get; set; }
        public int ReplacedCount { get; set; }
        public int SkippedCount { get; set; }

        public int ChangedCount => AddedCount + ReplacedCount;
    }

    private async void CreateNewProjectExecute()
    {
        var request = _projectUiService.BuildCreateProjectDialogRequest();
        if (!_projectUiService.TryGetCreateProjectInput(request, out var createResult))
        {
            return;
        }

        _projectUiService.SaveCreateProjectDialogPreferences(request, createResult);

        var targetProjectFolder = BuildTargetProjectFolderPath(createResult.ProjectFolder, createResult.ProjectName);
        if (Directory.Exists(targetProjectFolder))
        {
            _projectUiService.ShowWarning(
                $"A folder named '{Path.GetFileName(targetProjectFolder)}' already exists in:\n{createResult.ProjectFolder}\n\nChoose a different project name or folder.",
                "Create Project");
            return;
        }

        var orchestrator = _shellOrchestrator;
        if (orchestrator is null)
        {
            try
            {
                var projectFilePath = CreateProjectFromStarterWorkflow(
                    createResult.ProjectFolder,
                    createResult.ProjectName,
                    createResult.StarterProjectFilePath,
                    createResult.GameDisplayName,
                    createResult.GameSummary,
                    createResult.GamePreviewImages);
                RunAutomaticBaselineValidationWorkflow();
                _projectUiService.ShowInformation($"Project created at:\n{projectFilePath}", "Create Project");
            }
            catch (Exception ex)
            {
                _projectUiService.ShowError($"Unable to create project.\n{ex.Message}", "Create Project");
            }

            return;
        }

        var result = await orchestrator.CreateProjectAsync(
                new CreateProjectWorkflowRequest(
                    BaseFolder: createResult.ProjectFolder,
                    ProjectName: createResult.ProjectName,
                    StarterProjectFilePath: createResult.StarterProjectFilePath,
                    GameDisplayName: createResult.GameDisplayName,
                    GameSummary: createResult.GameSummary,
                    GamePreviewImages: createResult.GamePreviewImages),
            CancellationToken.None);

        if (!result.Succeeded)
        {
            var message = result.Error?.TechnicalDetail
                ?? result.Error?.UserMessage
                ?? "Unable to create project.";
            _projectUiService.ShowError($"Unable to create project.\n{message}", "Create Project");
            return;
        }

        _projectUiService.ShowInformation($"Project created at:\n{result.Payload!.ProjectFilePath}", "Create Project");
    }

    internal string CreateProjectFromStarterWorkflow(
        string projectFolder,
        string projectName,
        string? starterProjectFilePath,
        string gameDisplayName,
        string gameSummary,
        List<string> gamePreviewImages)
    {
        var normalizedFolder = projectFolder.Trim();
        var normalizedName = projectName.Trim();

        var targetProjectFolder = BuildTargetProjectFolderPath(normalizedFolder, normalizedName);
        if (Directory.Exists(targetProjectFolder))
        {
            throw new InvalidOperationException($"Target folder already exists: {targetProjectFolder}");
        }

        var normalizedGameDisplayName = gameDisplayName?.Trim() ?? string.Empty;
        var normalizedGameSummary = gameSummary?.Trim() ?? string.Empty;
        var normalizedGamePreviewImages = (gamePreviewImages ?? new List<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var model = string.IsNullOrWhiteSpace(starterProjectFilePath)
            ? CreateBlankProjectModel(normalizedName, normalizedGameDisplayName, normalizedGameSummary, normalizedGamePreviewImages)
            : LoadStarterProjectModel(starterProjectFilePath, normalizedName, normalizedGameDisplayName, normalizedGameSummary, normalizedGamePreviewImages);

        string? createdProjectFilePath = null;
        try
        {
            createdProjectFilePath = _jsonExportService.CreateProjectSkeleton(normalizedFolder, normalizedName);
            _jsonExportService.SaveProjectModel(createdProjectFilePath, model);
            _jsonExportService.ExportCleanProjectV1(createdProjectFilePath, model);

            if (!OpenProject(createdProjectFilePath))
            {
                throw new InvalidOperationException("Created project could not be opened after bootstrap.");
            }

            return createdProjectFilePath;
        }
        catch
        {
            TryRollbackCreatedProjectFolder(targetProjectFolder);
            throw;
        }
    }

    private ProjectModel LoadStarterProjectModel(
        string starterProjectFilePath,
        string projectName,
        string gameDisplayName,
        string gameSummary,
        List<string> gamePreviewImages)
    {
        if (!File.Exists(starterProjectFilePath))
        {
            throw new FileNotFoundException("Starter project file was not found.", starterProjectFilePath);
        }

        var loaded = _jsonExportService.TryLoadProjectModel(starterProjectFilePath);
        if (loaded is null)
        {
            throw new InvalidOperationException("Starter project could not be loaded. The starter file may be invalid or corrupted.");
        }

        loaded.Name = projectName;
        loaded.GameDisplayName = gameDisplayName;
        loaded.GameSummary = gameSummary;
        loaded.GamePreviewImages = gamePreviewImages;
        ScopeHierarchy.AttachParents(loaded);
        return loaded;
    }

    private static ProjectModel CreateBlankProjectModel(
        string projectName,
        string gameDisplayName,
        string gameSummary,
        List<string> gamePreviewImages)
    {
        var project = new ProjectModel
        {
            Name = projectName,
            GameDisplayName = gameDisplayName,
            GameSummary = gameSummary,
            GamePreviewImages = gamePreviewImages,
            DefaultTraversalMode = AreaAdjacencyMode.EightDirectional,
            CommandVerbs = new List<string>(),
            Directionals = new List<string>(),
            DirectionalTraversalMappings = new List<DirectionalTraversalMapping>(),
            GlobalVariables = new List<GamePropertyDefinition>(),
            SharedVariables = new List<SharedVariableDefinition>(),
            GameObjects = new List<GameObject>(),
            ObjectTemplates = new List<GameObject>(),
            RoomTemplates = new List<Room>(),
            BaseObjects = new List<GameObject>(),
            UiState = new ProjectUiState(),
            Planets = new List<Planet>()
        };

        project.GlobalScope.Name = "Global Objects";
        project.GlobalScope.GameProperties = new List<GamePropertyDefinition>();
        project.GlobalScope.AvailableActions = new List<CommandAction>();
        return project;
    }

    private static string BuildTargetProjectFolderPath(string baseFolder, string projectName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedProjectName = new string(projectName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return Path.Combine(baseFolder, sanitizedProjectName);
    }

    private static void TryRollbackCreatedProjectFolder(string targetProjectFolder)
    {
        try
        {
            if (Directory.Exists(targetProjectFolder))
            {
                Directory.Delete(targetProjectFolder, recursive: true);
            }
        }
        catch
        {
        }
    }

    internal void RunAutomaticBaselineValidationWorkflow()
    {
        var contextNode = HierarchyRoots.FirstOrDefault() ?? SelectedNode;
        if (contextNode is null)
        {
            return;
        }

        ValidateProjectWithScope(contextNode, ValidationExecutionKind.WholeProject, TreeValidationCompletionMode.FullReport);
    }

    private async void OpenProjectExecute()
    {
        if (!_projectUiService.TryGetOpenProjectPath(out var projectPath))
        {
            return;
        }

        var orchestrator = _shellOrchestrator;
        if (orchestrator is null)
        {
            if (!OpenProject(projectPath))
            {
                _projectUiService.ShowWarning("The selected file is not a valid storyboard project file.", "Open Project");
            }

            return;
        }

        var result = await orchestrator.OpenProjectAsync(
                new OpenProjectWorkflowRequest(projectPath),
                CancellationToken.None);
        if (!result.Succeeded)
        {
            _projectUiService.ShowWarning("The selected file is not a valid storyboard project file.", "Open Project");
        }
    }

    private async void SaveProjectExecute()
    {
        var orchestrator = _shellOrchestrator;
        if (orchestrator is null)
        {
            if (!SaveProject())
            {
                _projectUiService.ShowWarning(ExportStatus, "Save & Export");
            }

            return;
        }

        var result = await orchestrator.SaveProjectAsync(new SaveProjectWorkflowRequest(), CancellationToken.None);
        if (!result.Succeeded)
        {
            _projectUiService.ShowWarning(ExportStatus, "Save & Export");
        }
    }

    private async void SaveProjectAsExecute()
    {
        if (!_projectUiService.TryGetSaveAsProjectInput(out var projectFolder, out var projectName))
        {
            return;
        }

        var orchestrator = _shellOrchestrator;
        if (orchestrator is null)
        {
            if (!SaveProjectAs(projectFolder, projectName))
            {
                _projectUiService.ShowWarning(ExportStatus, "Save As");
                return;
            }

            _projectUiService.ShowInformation($"Project saved as:\n{_projectFilePath}", "Save As");
            return;
        }

        var result = await orchestrator.SaveProjectAsAsync(
                new SaveProjectAsWorkflowRequest(projectFolder, projectName),
                CancellationToken.None);
        if (!result.Succeeded)
        {
            _projectUiService.ShowWarning(ExportStatus, "Save As");
            return;
        }

        _projectUiService.ShowInformation($"Project saved as:\n{result.Payload!.ProjectFilePath}", "Save As");
    }

    private void ExportCleanProjectExecute()
    {
        if (!TryExportCleanProject(out var cleanProjectPath, out var error))
        {
            _projectUiService.ShowWarning(error, "Export Runtime JSON");
            return;
        }

        _projectUiService.ShowInformation($"Runtime project export written to:\n{cleanProjectPath}", "Export Runtime JSON");
    }

    private void ValidateProjectExecute()
    {
        var contextNode = HierarchyRoots.FirstOrDefault() as HierarchyNodeViewModel
            ?? SelectedNode;
        if (contextNode is null)
        {
            return;
        }

        ValidateProjectWithScope(
            contextNode,
            ValidationExecutionKind.WholeProject,
            TreeValidationCompletionMode.FullReport);
    }

    private async void OpenRecentProjectExecute(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        var orchestrator = _shellOrchestrator;
        if (orchestrator is null)
        {
            if (!OpenProject(projectPath))
            {
                _projectUiService.ShowWarning("Unable to open the selected recent project.", "Open Recent");
            }

            return;
        }

        var result = await orchestrator.OpenProjectAsync(
                new OpenProjectWorkflowRequest(projectPath),
                CancellationToken.None);
        if (!result.Succeeded)
        {
            _projectUiService.ShowWarning("Unable to open the selected recent project.", "Open Recent");
        }
    }

    private ValidationIssueBuildResult BuildProjectValidationIssues(
        ValidationExecutionRequest request,
        bool includeImagePathAvailabilityRule = true)
    {
        var executionResult = BuildEngineValidationResult(includeImagePathAvailabilityRule);
        var processingResult = ValidationIssueRunProcessor.Process(_project, executionResult.Issues, request);
        return new ValidationIssueBuildResult(processingResult, executionResult);
    }

    private ValidationExecutionResult BuildEngineValidationResult(bool includeImagePathAvailabilityRule = true)
    {
        var registry = CreateValidationRuleRegistry(includeImagePathAvailabilityRule);
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(
            _project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport,
            ProjectFilePath: _projectFilePath));
    }

    private static ValidationRuleRegistry CreateValidationRuleRegistry(bool includeImagePathAvailabilityRule = true)
    {
        var registry = new ValidationRuleRegistry();

        // Slice A + current Slice B extraction set.
        registry.Register(new DuplicateNameInScopeRule());
        registry.Register(new AdjacentRoomDuplicateNormalizedNameWarningRule());
        registry.Register(new InvalidStartingScopeRule());
        registry.Register(new SharedVariableSingleMembershipRule());
        registry.Register(new SharedVariableInitialValueConsistencyRule());
        registry.Register(new SharedVariableValueRestrictionConsistencyRule());
        registry.Register(new SharedVariableLegacyParticipantDriftRule());
        registry.Register(new SharedVariableOrphanMetadataShellRule());
        registry.Register(new SharedVariableDuplicateDisplayNameRule());
        registry.Register(new ProcedureParticipantObjectReferenceExistsRule());
        registry.Register(new ProcedureRequiresMutationRule());
        registry.Register(new ProcedureOwnershipReferenceExistsRule());
        registry.Register(new EventSubscriptionIntegrityRule());
        registry.Register(new EventSubscriptionManifestEventKeyRule());
        registry.Register(new EventSubscriptionFilterVariableSyntaxRule());
        registry.Register(new EventSubscriptionAnchorSubPropertyReferenceRule());
        registry.Register(new EventSubscriptionDuplicateFilterConditionRule());
        registry.Register(new EventSubscriptionAmbiguousAnchorLikePathRule());
        registry.Register(new EventSubscriptionAnchorDynamicTailReferenceRule());
        registry.Register(new TimerDefinitionOwnerScopeContextRule());
        registry.Register(new PhaseAmbientTimerKeyCrossTierWarningRule());
        registry.Register(new ScopedActionNameUniquenessRule());
        registry.Register(new RuntimeExportIdUniquenessRule());
        registry.Register(new SoundEffectIdentityUniquenessWarningRule());
        registry.Register(new SoundEffectDurationPopulatedRule());
        registry.Register(new SinglePlayerMarkerRule());
        registry.Register(new TraversalRequireOpenSharedIsOpenRule());
        registry.Register(new TraversalConnectionIntegrityRule());
        registry.Register(new TraversalDirectionalTargetUniquenessRule());
        registry.Register(new TraversalLegPassableContractRule());
        registry.Register(new TraversalDoorLinkIntegrityRule());
        registry.Register(new TraversalDoorOpenStateLinkConventionRule());
        registry.Register(new RoomPlacementReferenceIntegrityRule());
        registry.Register(new RoomCanvasDimensionsRule());
        if (includeImagePathAvailabilityRule)
        {
            registry.Register(new ImagePathAvailabilityRule());
        }
        registry.Register(new InventoriableGlobalUniquenessRule());
        registry.Register(new CompositeObjectRequiredPartsExistRule());
        registry.Register(new LinkedBaseObjectReferenceExistsRule());
        registry.Register(new LinkedBaseObjectSingleLevelRule());
        registry.Register(new RoomObjectPreviewPlacementRule());
        registry.Register(new RoomObjectPreviewCoordinateBoundsRule());
        registry.Register(new SynonymRequiresTargetRule());
        registry.Register(new SynonymSelfTargetRule());
        registry.Register(new SynonymMissingTargetRule());
        registry.Register(new LinkedActionSelfTargetRule());
        registry.Register(new LinkedActionMissingTargetRule());
        registry.Register(new LinkedFlowTargetsSynonymRule());
        registry.Register(new SynonymUsedInLinkedFlowRule());
        registry.Register(new LinkedActionCycleRule());
        registry.Register(new CompositeActionMissingTargetRule());
        registry.Register(new CompositeActionMissingRequiredPartRule());
        registry.Register(new BuildCompositeByPartsMissingTargetObjectRule());
        registry.Register(new BuildCompositeByPartsTargetRecipeMismatchRule());
        registry.Register(new InvokeProcedureMissingTargetRule());
        registry.Register(new MaterializeSourceObjectReferenceExistsRule());
        registry.Register(new OutcomeSoundEffectReferenceExistsRule());
        registry.Register(new ScriptUnknownReferenceRule());
        registry.Register(new ScriptMalformedIndexedReferenceRule());
        registry.Register(new ScriptIfElseBlockIntegrityRule());

        return registry;
    }

    private bool ValidateProjectWithScope(
        HierarchyNodeViewModel contextNode,
        ValidationExecutionKind executionKind,
        TreeValidationCompletionMode? completionModeOverride = null)
    {
        var completionMode = completionModeOverride ?? GetLastTreeValidationCompletionMode();
        var includeDescendants = executionKind != ValidationExecutionKind.ScopedNodeOnly;
        var rootScope = executionKind == ValidationExecutionKind.WholeProject
            ? null
            : ResolveScopeNodeForHierarchyNode(contextNode);

        var request = new ValidationExecutionRequest(
            _project,
            executionKind,
            rootScope,
            includeDescendants,
            completionMode == TreeValidationCompletionMode.StopOnFirstBlocking
                ? ValidationCompletionMode.StopOnFirstBlocking
                : ValidationCompletionMode.FullReport,
            _projectFilePath);

        var buildResult = BuildProjectValidationIssues(request);
        var processingResult = buildResult.ProcessingResult;
        var executionResult = buildResult.ExecutionResult;
        var issues = processingResult.Issues.Select(MapValidationIssue).ToList();
        var stoppedEarly = processingResult.StoppedEarly;
        var remainingIssueCountAtStop = processingResult.RemainingIssueCountAtStop;

        if (issues.Count > 0)
        {
            ApplyValidationErrorsToScopeNodes(
                issues,
                BuildValidationRunSummary(executionKind, completionMode, executionResult));
        }
        else
        {
            ClearScopeNodeValidationErrors(BuildValidationRunSummary(executionKind, completionMode, executionResult));
        }

        _project.UiState.LastTreeValidationActionId = executionKind switch
        {
            ValidationExecutionKind.ScopedNodeOnly => ValidateNodeOnlyActionId,
            ValidationExecutionKind.ScopedFromNode when completionMode == TreeValidationCompletionMode.StopOnFirstBlocking => ValidateFromHereStopOnFirstBlockingActionId,
            ValidationExecutionKind.ScopedFromNode => ValidateFromHereActionId,
            _ => ValidateWholeProjectActionId
        };
        _project.UiState.LastTreeValidationCompletionMode = completionMode.ToString();
        PersistUiStateSidecarIfPossible();

        var scopeLabel = executionKind switch
        {
            ValidationExecutionKind.ScopedNodeOnly => "node-only",
            ValidationExecutionKind.ScopedFromNode => "from-here",
            _ => "whole-project"
        };

        if (issues.Count == 0)
        {
            ExportStatus = $"Project validation passed ({scopeLabel}, {completionMode}).";
            return true;
        }

        var reportFilePath = WriteValidationReportFile(issues, executionResult);
        _projectUiService.ShowValidationReport("Validation Report", issues, reportFilePath, NavigateToValidationIssue);

        var errorCount = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var warningCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        ExportStatus = $"Project validation found {issues.Count} issue(s) for {scopeLabel} ({completionMode}): {errorCount} error(s), {warningCount} warning(s).";
        if (stoppedEarly)
        {
            ExportStatus += $" Stopped early with {remainingIssueCountAtStop} issue(s) remaining.";
        }

        return true;
    }

    private TreeValidationCompletionMode GetLastTreeValidationCompletionMode()
    {
        if (Enum.TryParse<TreeValidationCompletionMode>(_project.UiState.LastTreeValidationCompletionMode, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return TreeValidationCompletionMode.FullReport;
    }

    private static string BuildValidationRunSummary(
        ValidationExecutionKind executionKind,
        TreeValidationCompletionMode completionMode,
        ValidationExecutionResult executionResult)
    {
        var scopeLabel = executionKind switch
        {
            ValidationExecutionKind.ScopedNodeOnly => "NodeOnly",
            ValidationExecutionKind.ScopedFromNode => "Subtree",
            _ => "FullProject"
        };

        var earlyStop = executionResult.StoppedEarly
            ? $" StoppedEarly=true RemainingAtStop={executionResult.RemainingIssueCountAtStop} StopRuleId={executionResult.StopRuleId ?? "(none)"} RemainingRulesAtStop={executionResult.RemainingRuleCountAtStop}."
            : " StoppedEarly=false.";

        var ruleSummary = $" ExecutedRules={executionResult.ExecutedRuleIds.Count} SkippedRules={executionResult.SkippedRules.Count}.";

        return $"Last validation: Scope={scopeLabel}, Mode={completionMode}.{earlyStop}{ruleSummary}";
    }

    private ScopeNodeBase? ResolveScopeNodeForHierarchyNode(HierarchyNodeViewModel node)
    {
        return node switch
        {
            ProjectRootNodeViewModel => null,
            PlanetNodeViewModel planetNode => planetNode.Planet,
            CountryNodeViewModel countryNode => countryNode.Country,
            AreaNodeViewModel areaNode => areaNode.Area,
            RoomNodeViewModel roomNode => roomNode.Room,
            RoomTemplatesNodeViewModel => new RoomTemplatesScopeNode(_project),
            TemplateRoomNodeViewModel roomNode => roomNode.Room,
            GameObjectNodeViewModel objectNode => objectNode.GameObject,
            GlobalObjectNodeViewModel objectNode => objectNode.GameObject,
            TemplateGameObjectNodeViewModel objectNode => objectNode.GameObject,
            RoomGameObjectsNodeViewModel objectsNode => objectsNode.RoomNode.Room,
            ScopedActionsNodeViewModel scopedActionsNode when scopedActionsNode.Parent is not null => ResolveScopeNodeForHierarchyNode(scopedActionsNode.Parent),
            ScopedActionEntryNodeViewModel scopedActionNode => ResolveScopeNodeForHierarchyNode(scopedActionNode.ParentActionsNode),
            ScopedVerbsNodeViewModel scopedVerbsNode when scopedVerbsNode.Parent is not null => ResolveScopeNodeForHierarchyNode(scopedVerbsNode.Parent),
            ScopedDirectionalsNodeViewModel scopedDirectionalsNode when scopedDirectionalsNode.Parent is not null => ResolveScopeNodeForHierarchyNode(scopedDirectionalsNode.Parent),
            ScopedProceduresNodeViewModel scopedProceduresNode when scopedProceduresNode.Parent is not null => ResolveScopeNodeForHierarchyNode(scopedProceduresNode.Parent),
            GamePropertiesContainerNodeViewModel variablesNode when variablesNode.Parent is not null => ResolveScopeNodeForHierarchyNode(variablesNode.Parent),
            GamePropertyNodeViewModel variableNode => ResolveScopeNodeForHierarchyNode(variableNode.VariablesContainer),
            RoomTraversalLegsNodeViewModel traversalLegsNode => traversalLegsNode.RoomNode.Room,
            TraversalLegNodeViewModel traversalLegNode => traversalLegNode.RoomNode.Room,
            PlanetSettingsNodeViewModel planetSettingsNode => planetSettingsNode.PlanetNode.Planet,
            CountrySettingsNodeViewModel countrySettingsNode => countrySettingsNode.CountryNode.Country,
            AreaSettingsNodeViewModel areaSettingsNode => areaSettingsNode.AreaNode.Area,
            RoomSettingsNodeViewModel roomSettingsNode => roomSettingsNode.Room,
            GameObjectSettingsNodeViewModel objectSettingsNode => ResolveScopeNodeForHierarchyNode(objectSettingsNode.ObjectNode),
            GroupContainerNodeViewModel groupNode => ResolveScopeNodeForHierarchyNode(groupNode.OwnerNode),
            _ when node.Parent is not null => ResolveScopeNodeForHierarchyNode(node.Parent),
            _ => null
        };
    }

    private static ProjectValidationIssue MapValidationIssue(ValidationIssue issue)
    {
        return new ProjectValidationIssue(
            issue.Severity == ValidationSeverity.Warning ? ValidationSeverity.Warning : ValidationSeverity.Error,
            issue.Path,
            issue.Description,
            issue.Hint,
            issue.RuleId);
    }


    private string WriteValidationReportFile(IReadOnlyList<ProjectValidationIssue> issues, ValidationExecutionResult executionResult)
    {
        var basePath = string.IsNullOrWhiteSpace(_projectFilePath)
            ? Path.Combine(Environment.CurrentDirectory, "Storyboard.validation.txt")
            : Path.Combine(
                Path.GetDirectoryName(_projectFilePath) ?? Environment.CurrentDirectory,
                Path.GetFileNameWithoutExtension(_projectFilePath) + ".validation.txt");

        var lines = new List<string>
        {
            $"Validation report generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Project: {_project.Name}",
            $"ExecutionKind: {executionResult.ExecutionKind}",
            $"RootScopePath: {executionResult.RootScopePath}",
            $"IncludeDescendants: {executionResult.IncludeDescendants}",
            $"CandidateNodeCount: {executionResult.CandidateNodeCount}",
            $"ExecutedRules: {executionResult.ExecutedRuleIds.Count}",
            $"SkippedRules: {executionResult.SkippedRules.Count}",
            $"StoppedEarly: {executionResult.StoppedEarly}",
            $"RemainingIssueCountAtStop: {executionResult.RemainingIssueCountAtStop}",
            $"StopRuleId: {executionResult.StopRuleId ?? "(none)"}",
            $"RemainingRuleCountAtStop: {executionResult.RemainingRuleCountAtStop}",
            string.Empty,
            "SkippedRuleId\tSkipReason\tSkipDetail",
            string.Empty,
            "RuleId\tSeverity\tPath\tDescription\tHint"
        };

        lines.InsertRange(
            lines.Count - 2,
            executionResult.SkippedRules.Select(skipped =>
                $"{skipped.RuleId}\t{skipped.SkipReason}\t{(skipped.SkipDetail ?? string.Empty)}"));

        foreach (var issue in issues)
        {
            var hint = string.IsNullOrWhiteSpace(issue.Hint) ? string.Empty : issue.Hint;
            lines.Add($"{issue.RuleId}\t{issue.Severity}\t{issue.Path}\t{issue.Description}\t{hint}");
        }

        try
        {
            File.WriteAllLines(basePath, lines);
            return basePath;
        }
        catch
        {
            var fallback = Path.Combine(Path.GetTempPath(), $"Storyboard.validation.{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllLines(fallback, lines);
            return fallback;
        }
    }

    private sealed record ValidationIssueBuildResult(
        ValidationIssueRunProcessingResult ProcessingResult,
        ValidationExecutionResult ExecutionResult);

    private void ApplyValidationErrorsToScopeNodes(IReadOnlyList<ProjectValidationIssue> validationIssues)
    {
        ApplyValidationErrorsToScopeNodes(validationIssues, "Last validation: Scope=FullProject, Mode=FullReport. StoppedEarly=false.");
    }

    private void ApplyValidationErrorsToScopeNodes(IReadOnlyList<ProjectValidationIssue> validationIssues, string runSummary)
    {
        var scopedNodes = EnumerateScopeNodes().ToList();
        foreach (var scopeNode in scopedNodes)
        {
            scopeNode.ValidationErrors = Array.Empty<string>();
        }

        var errorsByNode = new Dictionary<ScopeNodeBase, List<string>>();
        var pathToNode = BuildScopePathLookup(scopedNodes);

        foreach (var issue in validationIssues.Where(issue => issue.Severity == ValidationSeverity.Error))
        {
            var targetNode = ResolveScopeNodeForIssuePath(issue.Path, pathToNode);
            if (targetNode is null)
            {
                continue;
            }

            var issueText = string.IsNullOrWhiteSpace(issue.Hint)
                ? issue.Description
                : $"{issue.Description} Fix: {issue.Hint}";

            if (!errorsByNode.TryGetValue(targetNode, out var nodeErrors))
            {
                nodeErrors = new List<string>();
                errorsByNode[targetNode] = nodeErrors;
            }

            if (!nodeErrors.Contains(issueText, StringComparer.OrdinalIgnoreCase))
            {
                nodeErrors.Add(issueText);
            }
        }

        foreach (var (node, nodeErrors) in errorsByNode)
        {
            node.ValidationErrors = nodeErrors;
        }

        _latestValidationIssues = validationIssues.ToList();
        _latestValidationTimestamp = DateTime.UtcNow;
        _latestValidationRunSummary = runSummary;
        _staleValidationNodePaths.Clear();
        RefreshHierarchyValidationProjection();
        RefreshAreaTraversalValidationProjection();
    }

    private void ClearScopeNodeValidationErrors()
    {
        ClearScopeNodeValidationErrors("Last validation: Scope=FullProject, Mode=FullReport. StoppedEarly=false.");
    }

    private void ClearScopeNodeValidationErrors(string runSummary)
    {
        foreach (var scopeNode in EnumerateScopeNodes())
        {
            scopeNode.ValidationErrors = Array.Empty<string>();
        }

        _latestValidationIssues = Array.Empty<ProjectValidationIssue>();
        _latestValidationTimestamp = DateTime.UtcNow;
        _latestValidationRunSummary = runSummary;
        _staleValidationNodePaths.Clear();
        RefreshHierarchyValidationProjection();
        RefreshAreaTraversalValidationProjection();
    }

    private void RefreshAreaTraversalValidationProjection()
    {
        foreach (var editor in OpenAreaEditors)
        {
            editor.SetTraversalValidationIssues(_latestValidationIssues);
        }
    }

    private void RefreshHierarchyValidationProjection()
    {
        var hierarchyNodes = EnumerateHierarchyNodes(HierarchyRoots).ToList();
        if (hierarchyNodes.Count == 0)
        {
            return;
        }

        var directErrors = new Dictionary<HierarchyNodeViewModel, int>(ReferenceEqualityComparer.Instance);
        var directWarnings = new Dictionary<HierarchyNodeViewModel, int>(ReferenceEqualityComparer.Instance);
        var directIssueMessages = new Dictionary<HierarchyNodeViewModel, List<string>>(ReferenceEqualityComparer.Instance);
        var scopeLookup = BuildScopePathLookup(EnumerateScopeNodes().ToList());

        foreach (var issue in _latestValidationIssues)
        {
            var targetNode = ResolveHierarchyNodeForIssue(issue, scopeLookup);
            if (targetNode is null)
            {
                continue;
            }

            if (issue.Severity == ValidationSeverity.Error)
            {
                directErrors[targetNode] = directErrors.TryGetValue(targetNode, out var count) ? count + 1 : 1;
            }
            else
            {
                directWarnings[targetNode] = directWarnings.TryGetValue(targetNode, out var count) ? count + 1 : 1;
            }

            if (!directIssueMessages.TryGetValue(targetNode, out var messages))
            {
                messages = new List<string>();
                directIssueMessages[targetNode] = messages;
            }

            var issuePreview = BuildValidationIssuePreviewLine(issue);
            if (!messages.Contains(issuePreview, StringComparer.OrdinalIgnoreCase))
            {
                messages.Add(issuePreview);
            }
        }

        var summary = string.Equals(_latestValidationRunSummary, "Not yet validated.", StringComparison.Ordinal)
            ? _latestValidationRunSummary
            : $"{_latestValidationRunSummary} Timestamp={_latestValidationTimestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";

        foreach (var node in hierarchyNodes)
        {
            node.SetValidationCounts(0, 0, 0, 0);
            node.SetDirectValidationIssueMessages(Array.Empty<string>());
            node.IsValidationStatusStale = false;
            node.SetValidationRunSummary(summary);
        }

        var aggregateByNode = new Dictionary<HierarchyNodeViewModel, (int Errors, int Warnings)>(ReferenceEqualityComparer.Instance);
        foreach (var root in HierarchyRoots)
        {
            ComputeAggregates(root);
        }

        foreach (var node in hierarchyNodes)
        {
            var directErrorCount = directErrors.TryGetValue(node, out var directError) ? directError : 0;
            var directWarningCount = directWarnings.TryGetValue(node, out var directWarning) ? directWarning : 0;
            var aggregate = aggregateByNode.TryGetValue(node, out var counts)
                ? counts
                : (Errors: 0, Warnings: 0);

            node.SetValidationCounts(
                directErrorCount,
                directWarningCount,
                Math.Max(0, aggregate.Errors - directErrorCount),
                Math.Max(0, aggregate.Warnings - directWarningCount));

            node.SetDirectValidationIssueMessages(
                directIssueMessages.TryGetValue(node, out var nodeIssues)
                    ? nodeIssues
                    : Array.Empty<string>());
        }

        if (_staleValidationNodePaths.Contains("*"))
        {
            foreach (var node in hierarchyNodes)
            {
                node.IsValidationStatusStale = true;
            }

            return;
        }

        var pathByNode = hierarchyNodes.ToDictionary(node => node, BuildNodePath);
        foreach (var node in hierarchyNodes.Where(node => _staleValidationNodePaths.Contains(pathByNode[node])))
        {
            node.IsValidationStatusStale = true;
        }

        (int Errors, int Warnings) ComputeAggregates(HierarchyNodeViewModel node)
        {
            var totalErrors = directErrors.TryGetValue(node, out var directErrorCount) ? directErrorCount : 0;
            var totalWarnings = directWarnings.TryGetValue(node, out var directWarningCount) ? directWarningCount : 0;

            foreach (var child in node.Children)
            {
                var childTotals = ComputeAggregates(child);
                totalErrors += childTotals.Errors;
                totalWarnings += childTotals.Warnings;
            }

            aggregateByNode[node] = (totalErrors, totalWarnings);
            return (totalErrors, totalWarnings);
        }
    }

    private static string BuildValidationIssuePreviewLine(ProjectValidationIssue issue)
    {
        var severity = issue.Severity == ValidationSeverity.Warning ? "Warning" : "Error";
        var ruleId = string.IsNullOrWhiteSpace(issue.RuleId) ? "(no-rule)" : issue.RuleId;
        var description = string.IsNullOrWhiteSpace(issue.Description) ? "Validation issue" : issue.Description.Trim();
        return $"[{severity} {ruleId}] {description}";
    }

    internal bool ShowValidationIssuesForNode(HierarchyNodeViewModel node)
    {
        var scopeLookup = BuildScopePathLookup(EnumerateScopeNodes().ToList());
        var nodeIssues = _latestValidationIssues
            .Where(issue => ReferenceEquals(ResolveHierarchyNodeForIssue(issue, scopeLookup), node))
            .ToList();

        if (nodeIssues.Count == 0)
        {
            _projectUiService.ShowInformation("No validation issues are currently associated with this node.", "Node Validation Issues");
            return false;
        }

        _projectUiService.ShowValidationReport(
            $"Node Validation Issues - {node.EditableName}",
            nodeIssues,
            string.Empty,
            NavigateToValidationIssue);
        return true;
    }

    private void NavigateToValidationIssue(ProjectValidationIssue issue)
    {
        var scopeLookup = BuildScopePathLookup(EnumerateScopeNodes().ToList());
        var targetNode = ResolveHierarchyNodeForIssue(issue, scopeLookup);
        if (targetNode is null)
        {
            ExportStatus = $"Could not navigate to validation target for path '{issue.Path}'.";
            return;
        }

        SelectedNode = targetNode;
        ExportStatus = $"Selected validation target: {targetNode.EditableName}";
    }

    private HierarchyNodeViewModel? ResolveHierarchyNodeForIssue(
        ProjectValidationIssue issue,
        IReadOnlyDictionary<string, ScopeNodeBase> scopeLookup)
    {
        if (TryGetActionPathParts(issue.Path, out var actionScopePath, out var actionName))
        {
            var actionNode = ResolveHierarchyNodeForAction(actionScopePath, actionName, scopeLookup);
            if (actionNode is not null)
            {
                return actionNode;
            }
        }

        var scopeNode = ResolveScopeNodeForIssuePath(issue.Path, scopeLookup);
        return scopeNode is null ? null : ResolveHierarchyNodeForScope(scopeNode);
    }

    private static bool TryGetActionPathParts(string path, out string scopePath, out string actionName)
    {
        scopePath = string.Empty;
        actionName = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        const string marker = " / Action:";
        var markerIndex = path.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return false;
        }

        scopePath = path[..markerIndex].Trim();
        actionName = path[(markerIndex + marker.Length)..].Trim();
        return !string.IsNullOrWhiteSpace(scopePath) && !string.IsNullOrWhiteSpace(actionName);
    }

    private HierarchyNodeViewModel? ResolveHierarchyNodeForAction(
        string actionScopePath,
        string actionName,
        IReadOnlyDictionary<string, ScopeNodeBase> scopeLookup)
    {
        var actionScope = ResolveScopeNodeForIssuePath(actionScopePath, scopeLookup);
        if (actionScope is null)
        {
            return null;
        }

        foreach (var node in EnumerateHierarchyNodes(HierarchyRoots))
        {
            if (node is ScopedActionEntryNodeViewModel scopedActionNode
                && string.Equals(scopedActionNode.EditableName, actionName, StringComparison.OrdinalIgnoreCase)
                && ReferenceEquals(ResolveScopeNodeForHierarchyNode(scopedActionNode), actionScope))
            {
                return scopedActionNode;
            }
        }

        return null;
    }

    private HierarchyNodeViewModel? ResolveHierarchyNodeForScope(ScopeNodeBase scope)
    {
        return scope switch
        {
            Planet planet => EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<PlanetNodeViewModel>()
                .FirstOrDefault(node => ReferenceEquals(node.Planet, planet)),
            Country country => EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<CountryNodeViewModel>()
                .FirstOrDefault(node => ReferenceEquals(node.Country, country)),
            Area area => EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<AreaNodeViewModel>()
                .FirstOrDefault(node => ReferenceEquals(node.Area, area)),
            Room room => ResolveHierarchyNodeForRoomScope(room),
            GameObject gameObject => ResolveHierarchyNodeForGameObject(gameObject),
            _ => null
        };
    }

    private HierarchyNodeViewModel? ResolveHierarchyNodeForRoomScope(Room room)
    {
        var roomNode = EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .FirstOrDefault(node => ReferenceEquals(node.Room, room));
        if (roomNode is not null)
        {
            return roomNode;
        }

        return EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<TemplateRoomNodeViewModel>()
            .FirstOrDefault(node => ReferenceEquals(node.Room, room));
    }

    private HierarchyNodeViewModel? ResolveHierarchyNodeForGameObject(GameObject gameObject)
    {
        foreach (var node in EnumerateHierarchyNodes(HierarchyRoots))
        {
            if (node is GameObjectNodeViewModel roomObjectNode && ReferenceEquals(roomObjectNode.GameObject, gameObject))
            {
                return roomObjectNode;
            }

            if (node is GlobalObjectNodeViewModel playerObjectNode && ReferenceEquals(playerObjectNode.GameObject, gameObject))
            {
                return playerObjectNode;
            }

            if (node is TemplateGameObjectNodeViewModel templateObjectNode && ReferenceEquals(templateObjectNode.GameObject, gameObject))
            {
                return templateObjectNode;
            }
        }

        return null;
    }

    private IEnumerable<ScopeNodeBase> EnumerateScopeNodes()
    {
        yield return _project;
        yield return new ObjectTemplatesScopeNode(_project);
        yield return new RoomTemplatesScopeNode(_project);
        yield return new BaseObjectsScopeNode(_project);

        foreach (var planet in _project.Planets)
        {
            yield return planet;
            yield return CreateScopeBaseObjectsNode(planet.BaseObjects, planet.BaseObjectsIgnoredValidationRuleIds, planet);

            foreach (var country in planet.Countries)
            {
                yield return country;
                yield return CreateScopeBaseObjectsNode(country.BaseObjects, country.BaseObjectsIgnoredValidationRuleIds, country);

                foreach (var area in country.Areas)
                {
                    yield return area;
                    yield return CreateScopeBaseObjectsNode(area.BaseObjects, area.BaseObjectsIgnoredValidationRuleIds, area);

                    foreach (var room in area.Rooms)
                    {
                        yield return room;

                        foreach (var roomObject in EnumerateGameObjects(room.GameObjects))
                        {
                            yield return roomObject;
                        }
                    }
                }
            }
        }

        foreach (var globalObject in EnumerateGameObjects(_project.GlobalScope.GameObjects))
        {
            yield return globalObject;
        }

        foreach (var templateObject in EnumerateGameObjects(_project.ObjectTemplates))
        {
            yield return templateObject;
        }

        foreach (var roomTemplate in _project.RoomTemplates)
        {
            yield return roomTemplate;

            foreach (var templateObject in EnumerateGameObjects(roomTemplate.GameObjects))
            {
                yield return templateObject;
            }
        }

        foreach (var baseObject in EnumerateGameObjects(_project.BaseObjects))
        {
            yield return baseObject;
        }
    }

    private static IEnumerable<GameObject> EnumerateGameObjects(IEnumerable<GameObject> roots)
    {
        foreach (var root in roots)
        {
            yield return root;

            foreach (var child in EnumerateGameObjects(root.ContainedObjects))
            {
                yield return child;
            }
        }
    }

    private sealed class ScopeAttachedBaseObjectsNode : ScopeNodeBase
    {
        private readonly List<GameObject> _objects;
        private readonly List<string> _ignoredValidationRuleIds;

        public ScopeAttachedBaseObjectsNode(List<GameObject> objects, List<string> ignoredValidationRuleIds, ScopeNodeBase parentScope)
        {
            _objects = objects;
            _ignoredValidationRuleIds = ignoredValidationRuleIds;
            ParentScope = parentScope;
        }

        public override ScopeNodeKind ScopeKind => ScopeNodeKind.Templates;
        public override string ScopeName => "Base Objects";
        public override IEnumerable<string> ScopeTokens => new[] { "base", "baseobjects" };
        public override List<string> IgnoredValidationRuleIds
        {
            get => _ignoredValidationRuleIds;
            set
            {
                _ignoredValidationRuleIds.Clear();
                if (value is null)
                {
                    return;
                }

                _ignoredValidationRuleIds.AddRange(value);
            }
        }

        public override IEnumerable<IScopedAwareNode> ChildScopes => _objects;

        protected override bool CanAcceptChild(IScopedAwareNode child)
        {
            return child.ScopeKind == ScopeNodeKind.GameObject;
        }

        protected override bool TryAddChildCore(IScopedAwareNode child)
        {
            if (child is not GameObject obj)
            {
                return false;
            }

            if (_objects.Contains(obj))
            {
                return true;
            }

            _objects.Add(obj);
            return true;
        }

        protected override bool TryRemoveChildCore(IScopedAwareNode child)
        {
            return child is GameObject obj && _objects.Remove(obj);
        }
    }

    private static ScopeNodeBase CreateScopeBaseObjectsNode(List<GameObject> objects, List<string> ignoredValidationRuleIds, ScopeNodeBase parentScope)
    {
        return new ScopeAttachedBaseObjectsNode(objects, ignoredValidationRuleIds, parentScope);
    }

    private Dictionary<string, ScopeNodeBase> BuildScopePathLookup(IEnumerable<ScopeNodeBase> nodes)
    {
        var map = new Dictionary<string, ScopeNodeBase>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            foreach (var alias in BuildScopePathAliases(node))
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                map.TryAdd(alias, node);
            }
        }

        return map;
    }

    private IEnumerable<string> BuildScopePathAliases(ScopeNodeBase node)
    {
        var names = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (names.Count == 0)
        {
            yield break;
        }

        yield return string.Join(" / ", names);

        if (node is ProjectModel)
        {
            yield return "Global";
        }

        if (node is ObjectTemplatesScopeNode)
        {
            yield return "Global / Object Templates";
            yield return "Global / Templates";
            yield return "Templates";
        }

        if (node is RoomTemplatesScopeNode)
        {
            yield return "Global / Room Templates";
            yield return "Room Templates";
        }

        if (node is BaseObjectsScopeNode)
        {
            yield return "Global / Base Objects";
            yield return "Base Objects";
        }

        if (node is GameObject && names.Count >= 2)
        {
            yield return string.Join(" / ", names.Skip(1));

            if (node.EnumerateSelfAndAncestors().Any(scope => scope is ObjectTemplatesScopeNode))
            {
                var templateParts = names.ToList();
                templateParts[1] = "Templates";
                yield return string.Join(" / ", templateParts);
                yield return string.Join(" / ", templateParts.Skip(1));

                if (templateParts.Count >= 3
                    && string.Equals(templateParts[0], "Global", StringComparison.OrdinalIgnoreCase))
                {
                    yield return string.Join(" / ", new[] { "Global" }.Concat(templateParts.Skip(2)));
                }
            }

            if (node.EnumerateSelfAndAncestors().Any(scope => scope is RoomTemplatesScopeNode))
            {
                var roomTemplateParts = names.ToList();
                roomTemplateParts[1] = "Room Templates";
                yield return string.Join(" / ", roomTemplateParts);
                yield return string.Join(" / ", roomTemplateParts.Skip(1));

                if (roomTemplateParts.Count >= 3
                    && string.Equals(roomTemplateParts[0], "Global", StringComparison.OrdinalIgnoreCase))
                {
                    yield return string.Join(" / ", new[] { "Global" }.Concat(roomTemplateParts.Skip(2)));
                }
            }

            if (node.EnumerateSelfAndAncestors().Any(scope => scope is BaseObjectsScopeNode))
            {
                var baseParts = names.ToList();
                baseParts[1] = "Base Objects";
                yield return string.Join(" / ", baseParts);
                yield return string.Join(" / ", baseParts.Skip(1));

                if (baseParts.Count >= 3
                    && string.Equals(baseParts[0], "Global", StringComparison.OrdinalIgnoreCase))
                {
                    yield return string.Join(" / ", new[] { "Global" }.Concat(baseParts.Skip(2)));
                }
            }
        }

        if (node is Area areaNode
            && areaNode.ParentScope is Country country
            && country.ParentScope is Planet planet)
        {
            yield return $"Project/{_project.Name}/Planet/{planet.ScopeName}/Country/{country.ScopeName}/Area/{areaNode.ScopeName}";
        }

        if (node is Country countryNode
            && countryNode.ParentScope is Planet parentPlanet)
        {
            yield return $"Project/{_project.Name}/Planet/{parentPlanet.ScopeName}/Country/{countryNode.ScopeName}";
        }

        if (node is Planet planetNode)
        {
            yield return $"Project/{_project.Name}/Planet/{planetNode.ScopeName}";
        }

        if (node is Room roomNode
            && roomNode.ParentScope is Area parentArea
            && parentArea.ParentScope is Country parentCountry
            && parentCountry.ParentScope is Planet grandPlanet)
        {
            yield return $"Project/{_project.Name}/Planet/{grandPlanet.ScopeName}/Country/{parentCountry.ScopeName}/Area/{parentArea.ScopeName}/Room/{roomNode.ScopeName}";
        }

        if (node is GameObject objectNode
            && objectNode.ParentScope is ScopeNodeBase parentScope
            && parentScope is ProjectModel)
        {
            yield return $"Global / {objectNode.ScopeName}";
        }
    }

    private static ScopeNodeBase? ResolveScopeNodeForIssuePath(string issuePath, IReadOnlyDictionary<string, ScopeNodeBase> pathToNode)
    {
        if (string.IsNullOrWhiteSpace(issuePath))
        {
            return null;
        }

        var normalizedPath = issuePath.Trim();
        if (pathToNode.TryGetValue(normalizedPath, out var exactMatch))
        {
            return exactMatch;
        }

        if (!normalizedPath.StartsWith("Global / ", StringComparison.OrdinalIgnoreCase)
            && pathToNode.TryGetValue($"Global / {normalizedPath}", out var prefixedMatch))
        {
            return prefixedMatch;
        }

        if (normalizedPath.StartsWith("Global / ", StringComparison.OrdinalIgnoreCase))
        {
            var unprefixed = normalizedPath["Global / ".Length..].Trim();
            if (pathToNode.TryGetValue(unprefixed, out var unprefixedMatch))
            {
                return unprefixedMatch;
            }
        }

        var best = pathToNode
            .Where(pair =>
                normalizedPath.StartsWith(pair.Key + " / ", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(pair.Key + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        return best;
    }


    private static void AddDuplicateNameErrors<T>(List<string> errors, IEnumerable<T> items, Func<T, string> selector, string scopeLabel, string itemLabel)
    {
        var groups = items
            .Select(item => selector(item)?.Trim() ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            errors.Add($"- Duplicate {itemLabel} name '{group.Key}' in {scopeLabel}.");
        }
    }

    private static void AddRoomObjectDuplicateNameErrors(List<string> errors, Room room)
    {
        var groups = room.GameObjects
            .Where(static obj =>
                !(obj.IsQuantifiable
                  && string.Equals(obj.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase)))
            .Select(static obj => obj.ScopeName?.Trim() ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            errors.Add($"- Duplicate game object name '{group.Key}' in room '{room.ScopeName}'.");
        }
    }
}





