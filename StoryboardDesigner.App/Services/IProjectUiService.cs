namespace StoryboardDesigner.App.Services;

/// <summary>Provides project dialogs, notifications, and external navigation for Designer workflows.</summary>
public interface IProjectUiService
{
    /// <summary>Builds the initial project creation dialog settings.</summary>
    /// <returns>The settings and available starter projects.</returns>
    CreateProjectDialogRequest BuildCreateProjectDialogRequest();

    /// <summary>Prompts for a new project's settings.</summary>
    /// <param name="request">The initial dialog settings.</param>
    /// <param name="result">The selected project settings.</param>
    /// <returns>Whether the user accepted the dialog.</returns>
    bool TryGetCreateProjectInput(CreateProjectDialogRequest request, out CreateProjectDialogResult result);

    /// <summary>Persists the accepted project creation preferences.</summary>
    /// <param name="request">The initial dialog settings.</param>
    /// <param name="result">The accepted settings.</param>
    void SaveCreateProjectDialogPreferences(CreateProjectDialogRequest request, CreateProjectDialogResult result);

    /// <summary>Prompts for a project copy destination.</summary>
    /// <param name="projectFolder">The selected parent folder.</param>
    /// <param name="projectName">The selected project name.</param>
    /// <returns>Whether the user selected a destination.</returns>
    bool TryGetSaveAsProjectInput(out string projectFolder, out string projectName);

    /// <summary>Prompts for an authored project to open.</summary>
    /// <param name="projectFilePath">The selected project path.</param>
    /// <returns>Whether the user selected a file.</returns>
    bool TryGetOpenProjectPath(out string projectFilePath);

    /// <summary>Prompts for a project supplying global definitions.</summary>
    /// <param name="projectFilePath">The selected source project path.</param>
    /// <returns>Whether the user selected a file.</returns>
    bool TryGetImportGlobalNodeProjectPath(out string projectFilePath);

    /// <summary>Prompts for the global definitions to import.</summary>
    /// <param name="request">The available definitions and initial settings.</param>
    /// <param name="selection">The accepted import selection.</param>
    /// <returns>Whether the user accepted the selection.</returns>
    bool TryGetImportGlobalNodeSelection(GlobalImportSelectionRequest request, out GlobalImportSelectionResult selection);

    /// <summary>Prompts for validation scope and completion settings.</summary>
    /// <param name="request">The initial validation settings.</param>
    /// <param name="selection">The accepted validation settings.</param>
    /// <returns>Whether the user accepted the settings.</returns>
    bool TryGetValidationRunSelection(ValidationRunDialogRequest request, out ValidationRunDialogResult selection);

    /// <summary>Displays an informational message.</summary>
    /// <param name="message">The message body.</param>
    /// <param name="title">The dialog title.</param>
    void ShowInformation(string message, string title);

    /// <summary>Displays a warning.</summary>
    /// <param name="message">The warning body.</param>
    /// <param name="title">The dialog title.</param>
    void ShowWarning(string message, string title);

    /// <summary>Displays an error.</summary>
    /// <param name="message">The error body.</param>
    /// <param name="title">The dialog title.</param>
    void ShowError(string message, string title);

    /// <summary>Requests confirmation for an operation.</summary>
    /// <param name="message">The confirmation body.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns>Whether the user confirmed.</returns>
    bool Confirm(string message, string title);

    /// <summary>Asks whether a workflow should continue after validation.</summary>
    /// <param name="workflowName">The operation being validated.</param>
    /// <param name="issueCount">The total issue count.</param>
    /// <param name="errorCount">The error count.</param>
    /// <param name="warningCount">The warning count.</param>
    /// <returns>Whether the user chose to continue.</returns>
    bool ConfirmContinueWithValidationSummary(string workflowName, int issueCount, int errorCount, int warningCount);

    /// <summary>Asks whether saving should continue after validation.</summary>
    /// <param name="errorCount">The error count.</param>
    /// <param name="warningCount">The warning count.</param>
    /// <param name="reportFilePath">The detailed report path.</param>
    /// <returns>Whether the user chose to save.</returns>
    bool ConfirmContinueSaveWithValidation(int errorCount, int warningCount, string reportFilePath);

    /// <summary>Displays validation issues and optional navigation.</summary>
    /// <param name="reportTitle">The report title.</param>
    /// <param name="issues">The issues to display.</param>
    /// <param name="reportFilePath">The exported report path.</param>
    /// <param name="navigateToIssue">Optional navigation callback.</param>
    void ShowValidationReport(
        string reportTitle,
        IReadOnlyList<ProjectValidationIssue> issues,
        string reportFilePath,
        Action<ProjectValidationIssue>? navigateToIssue = null);

    /// <summary>Prompts for saving before game state initialization.</summary>
    /// <returns>The user's save decision.</returns>
    AutoSaveBeforeGameStateInitResponse PromptAutoSaveBeforeGameStateInitialization();

    /// <summary>Prompts for a runtime variable value.</summary>
    /// <param name="variableName">The variable name.</param>
    /// <param name="currentValue">The current value.</param>
    /// <param name="value">The entered value.</param>
    /// <returns>Whether the user accepted the value.</returns>
    bool TryGetRuntimeVariableValue(string variableName, string currentValue, out string value);

    /// <summary>Prompts for an output log destination.</summary>
    /// <param name="filePath">The selected destination.</param>
    /// <returns>Whether the user selected a file.</returns>
    bool TryGetSaveOutputLogPath(out string filePath);

    /// <summary>Opens source image management.</summary>
    /// <param name="request">The initial image management settings.</param>
    /// <param name="result">The accepted changes.</param>
    /// <returns>Whether changes were accepted.</returns>
    bool TryRunSourceImageManagement(SourceImageManagementDialogRequest request, out SourceImageManagementDialogResult result);

    /// <summary>Opens a text file with its associated editor.</summary>
    /// <param name="filePath">The file to open.</param>
    /// <param name="errorMessage">Failure details when opening fails.</param>
    /// <returns>Whether the editor was started.</returns>
    bool TryOpenTextFileInDefaultEditor(string filePath, out string errorMessage);

    /// <summary>Opens a URI in the default browser.</summary>
    /// <param name="uri">The URI to open.</param>
    /// <param name="errorMessage">Failure details when opening fails.</param>
    /// <returns>Whether the browser was started.</returns>
    bool TryOpenUriInDefaultBrowser(string uri, out string errorMessage);

    /// <summary>Prompts for the echo message defaults to refresh.</summary>
    /// <param name="projectTargetAvailable">Whether a project target can be selected.</param>
    /// <param name="target">The selected target.</param>
    /// <returns>Whether the user selected a target.</returns>
    bool TryGetDefaultEchoMessagesRefreshTarget(bool projectTargetAvailable, out DefaultEchoMessagesRefreshTarget target);
}
