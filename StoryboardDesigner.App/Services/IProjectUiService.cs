namespace StoryboardDesigner.App.Services;

public interface IProjectUiService
{
    CreateProjectDialogRequest BuildCreateProjectDialogRequest();

    bool TryGetCreateProjectInput(CreateProjectDialogRequest request, out CreateProjectDialogResult result);

    void SaveCreateProjectDialogPreferences(CreateProjectDialogRequest request, CreateProjectDialogResult result);

    bool TryGetSaveAsProjectInput(out string projectFolder, out string projectName);

    bool TryGetOpenProjectPath(out string projectFilePath);

    bool TryGetImportGlobalNodeProjectPath(out string projectFilePath);

    bool TryGetImportGlobalNodeSelection(GlobalImportSelectionRequest request, out GlobalImportSelectionResult selection);

    bool TryGetValidationRunSelection(ValidationRunDialogRequest request, out ValidationRunDialogResult selection);

    void ShowInformation(string message, string title);

    void ShowWarning(string message, string title);

    void ShowError(string message, string title);

    bool Confirm(string message, string title);

    bool ConfirmContinueWithValidationSummary(string workflowName, int issueCount, int errorCount, int warningCount);

    bool ConfirmContinueSaveWithValidation(int errorCount, int warningCount, string reportFilePath);

    void ShowValidationReport(
        string reportTitle,
        IReadOnlyList<ProjectValidationIssue> issues,
        string reportFilePath,
        Action<ProjectValidationIssue>? navigateToIssue = null);

    AutoSaveBeforeGameStateInitResponse PromptAutoSaveBeforeGameStateInitialization();

    bool TryGetRuntimeVariableValue(string variableName, string currentValue, out string value);

    bool TryGetSaveGameSimulatorRecordingPath(out string filePath);

    bool TryGetOpenGameSimulatorRecordingPath(out string filePath);

    bool TryGetSaveOutputLogPath(out string filePath);

    bool TryRunSourceImageManagement(SourceImageManagementDialogRequest request, out SourceImageManagementDialogResult result);

    bool TryOpenTextFileInDefaultEditor(string filePath, out string errorMessage);

    bool TryOpenUriInDefaultBrowser(string uri, out string errorMessage);

    bool TryGetDefaultEchoMessagesRefreshTarget(bool projectTargetAvailable, out DefaultEchoMessagesRefreshTarget target);
}
