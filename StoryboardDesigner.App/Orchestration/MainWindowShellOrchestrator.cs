using StoryboardDesigner.App.Orchestration.Diagnostics;
using StoryboardDesigner.App.Orchestration.SaveGuard;
using StoryboardDesigner.App.Orchestration.Workflows;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Orchestration;

internal sealed class MainWindowShellOrchestrator : IMainWindowShellOrchestrator
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly IShellDiagnosticsSink _diagnosticsSink;
    private readonly ISaveGuardPolicyService _saveGuardPolicyService;
    private ShellStateSnapshot _currentState;

    public MainWindowShellOrchestrator(
        MainWindowViewModel mainWindowViewModel,
        IShellDiagnosticsSink diagnosticsSink,
        ISaveGuardPolicyService saveGuardPolicyService)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _diagnosticsSink = diagnosticsSink;
        _saveGuardPolicyService = saveGuardPolicyService;
        _currentState = BuildState(activeWorkflow: null, isBusy: false);
        _mainWindowViewModel.PropertyChanged += MainWindowViewModel_OnPropertyChanged;
    }

    public ShellStateSnapshot CurrentState => _currentState;

    public event EventHandler<ShellStateSnapshot>? StateChanged;

    public Task<ShellWorkflowResult<CreateProjectWorkflowResponse>> CreateProjectAsync(CreateProjectWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            PublishDiagnostic("CreateProject", ShellDiagnosticSeverity.Warning, "Canceled before execution.");
            return Task.FromResult(ShellWorkflowResult<CreateProjectWorkflowResponse>.Canceled());
        }

        PublishDiagnostic("CreateProject", ShellDiagnosticSeverity.Info, "Starting create workflow.");
        SetState(activeWorkflow: "CreateProject", isBusy: true);
        try
        {
            var projectFilePath = _mainWindowViewModel.CreateProjectFromStarterWorkflow(
                request.BaseFolder,
                request.ProjectName,
                request.StarterProjectFilePath,
                request.GameDisplayName,
                request.GameSummary,
                request.GamePreviewImages);
            _mainWindowViewModel.RunAutomaticBaselineValidationWorkflow();
            var response = new CreateProjectWorkflowResponse(projectFilePath, _mainWindowViewModel.Project.Name);
            PublishDiagnostic("CreateProject", ShellDiagnosticSeverity.Info, $"Create workflow completed: {projectFilePath}");
            return Task.FromResult(ShellWorkflowResult<CreateProjectWorkflowResponse>.Success(response));
        }
        catch (Exception ex)
        {
            PublishDiagnostic("CreateProject", ShellDiagnosticSeverity.Error, $"Create workflow failed: {ex.Message}");
            var error = new ShellWorkflowError(
                Category: ShellWorkflowFailureCategory.Unexpected,
                UserMessage: "Create project failed.",
                TechnicalDetail: ex.Message,
                IsRecoverable: true);
            return Task.FromResult(ShellWorkflowResult<CreateProjectWorkflowResponse>.Failure(error));
        }
        finally
        {
            SetState(activeWorkflow: null, isBusy: false);
        }
    }

    public Task<ShellWorkflowResult<OpenProjectWorkflowResponse>> OpenProjectAsync(OpenProjectWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            PublishDiagnostic("OpenProject", ShellDiagnosticSeverity.Warning, "Canceled before execution.");
            return Task.FromResult(ShellWorkflowResult<OpenProjectWorkflowResponse>.Canceled());
        }

        PublishDiagnostic("OpenProject", ShellDiagnosticSeverity.Info, $"Starting open workflow: {request.ProjectFilePath}");
        SetState(activeWorkflow: "OpenProject", isBusy: true);
        try
        {
            var opened = _mainWindowViewModel.OpenProject(request.ProjectFilePath);
            if (!opened)
            {
                var error = new ShellWorkflowError(
                    Category: ShellWorkflowFailureCategory.Validation,
                    UserMessage: "Open project failed.",
                    TechnicalDetail: _mainWindowViewModel.ExportStatus,
                    IsRecoverable: true);
                PublishDiagnostic("OpenProject", ShellDiagnosticSeverity.Warning, "Open workflow returned validation failure.");
                return Task.FromResult(ShellWorkflowResult<OpenProjectWorkflowResponse>.Failure(error));
            }

            var response = new OpenProjectWorkflowResponse(request.ProjectFilePath, _mainWindowViewModel.Project.Name);
            _mainWindowViewModel.RunAutomaticBaselineValidationWorkflow();
            PublishDiagnostic("OpenProject", ShellDiagnosticSeverity.Info, "Open workflow completed.");
            return Task.FromResult(ShellWorkflowResult<OpenProjectWorkflowResponse>.Success(response));
        }
        finally
        {
            SetState(activeWorkflow: null, isBusy: false);
        }
    }

    public Task<ShellWorkflowResult<SaveProjectWorkflowResponse>> SaveProjectAsync(SaveProjectWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            PublishDiagnostic("SaveProject", ShellDiagnosticSeverity.Warning, "Canceled before execution.");
            return Task.FromResult(ShellWorkflowResult<SaveProjectWorkflowResponse>.Canceled());
        }

        PublishDiagnostic("SaveProject", ShellDiagnosticSeverity.Info, "Starting save workflow.");
        SetState(activeWorkflow: "SaveProject", isBusy: true);
        try
        {
            var wasDirty = _mainWindowViewModel.IsProjectDirty;
            var saved = _mainWindowViewModel.SaveProject();
            if (!saved)
            {
                var error = new ShellWorkflowError(
                    Category: ShellWorkflowFailureCategory.Validation,
                    UserMessage: "Save project failed.",
                    TechnicalDetail: _mainWindowViewModel.ExportStatus,
                    IsRecoverable: true);
                PublishDiagnostic("SaveProject", ShellDiagnosticSeverity.Warning, "Save workflow returned validation failure.");
                return Task.FromResult(ShellWorkflowResult<SaveProjectWorkflowResponse>.Failure(error));
            }

            var response = new SaveProjectWorkflowResponse(_mainWindowViewModel.ProjectFilePath, wasDirty);
            PublishDiagnostic("SaveProject", ShellDiagnosticSeverity.Info, "Save workflow completed.");
            return Task.FromResult(ShellWorkflowResult<SaveProjectWorkflowResponse>.Success(response));
        }
        finally
        {
            SetState(activeWorkflow: null, isBusy: false);
        }
    }

    public Task<ShellWorkflowResult<SaveProjectAsWorkflowResponse>> SaveProjectAsAsync(SaveProjectAsWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            PublishDiagnostic("SaveProjectAs", ShellDiagnosticSeverity.Warning, "Canceled before execution.");
            return Task.FromResult(ShellWorkflowResult<SaveProjectAsWorkflowResponse>.Canceled());
        }

        PublishDiagnostic("SaveProjectAs", ShellDiagnosticSeverity.Info, "Starting save-as workflow.");
        SetState(activeWorkflow: "SaveProjectAs", isBusy: true);
        try
        {
            var saved = _mainWindowViewModel.SaveProjectAs(request.BaseFolder, request.ProjectName);
            if (!saved || string.IsNullOrWhiteSpace(_mainWindowViewModel.ProjectFilePath))
            {
                var error = new ShellWorkflowError(
                    Category: ShellWorkflowFailureCategory.Validation,
                    UserMessage: "Save project as failed.",
                    TechnicalDetail: _mainWindowViewModel.ExportStatus,
                    IsRecoverable: true);
                PublishDiagnostic("SaveProjectAs", ShellDiagnosticSeverity.Warning, "Save-as workflow returned validation failure.");
                return Task.FromResult(ShellWorkflowResult<SaveProjectAsWorkflowResponse>.Failure(error));
            }

            var response = new SaveProjectAsWorkflowResponse(_mainWindowViewModel.ProjectFilePath, _mainWindowViewModel.Project.Name);
            PublishDiagnostic("SaveProjectAs", ShellDiagnosticSeverity.Info, "Save-as workflow completed.");
            return Task.FromResult(ShellWorkflowResult<SaveProjectAsWorkflowResponse>.Success(response));
        }
        finally
        {
            SetState(activeWorkflow: null, isBusy: false);
        }
    }

    public Task<ShellWorkflowResult<CloseProjectWorkflowResponse>> CloseProjectAsync(CloseProjectWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            PublishDiagnostic("CloseProject", ShellDiagnosticSeverity.Warning, "Canceled before execution.");
            return Task.FromResult(ShellWorkflowResult<CloseProjectWorkflowResponse>.Canceled());
        }

        PublishDiagnostic("CloseProject", ShellDiagnosticSeverity.Info, "Starting close workflow.");
        SetState(activeWorkflow: "CloseProject", isBusy: true);
        try
        {
            var saveGuardDecision = _saveGuardPolicyService.EvaluateCloseProjectPolicy(
                _mainWindowViewModel.IsProjectDirty,
                request.SaveIfDirty);

            if (saveGuardDecision.Kind == CloseWorkflowSaveGuardDecisionKind.Block)
            {
                var blockedError = new ShellWorkflowError(
                    Category: ShellWorkflowFailureCategory.Validation,
                    UserMessage: "Close project blocked by save-guard policy.",
                    TechnicalDetail: saveGuardDecision.UserMessage,
                    IsRecoverable: true);
                PublishDiagnostic("CloseProject", ShellDiagnosticSeverity.Warning, "Close workflow blocked by save-guard policy.");
                return Task.FromResult(ShellWorkflowResult<CloseProjectWorkflowResponse>.Failure(blockedError));
            }

            if (saveGuardDecision.Kind == CloseWorkflowSaveGuardDecisionKind.AttemptSave && !_mainWindowViewModel.SaveProject())
            {
                var saveFailedError = new ShellWorkflowError(
                    Category: ShellWorkflowFailureCategory.Validation,
                    UserMessage: "Close project failed because save before close did not succeed.",
                    TechnicalDetail: _mainWindowViewModel.ExportStatus,
                    IsRecoverable: true);
                PublishDiagnostic("CloseProject", ShellDiagnosticSeverity.Warning, "Close workflow save-before-close failed.");
                return Task.FromResult(ShellWorkflowResult<CloseProjectWorkflowResponse>.Failure(saveFailedError));
            }

            var closed = _mainWindowViewModel.CloseProjectWorkflow();
            if (!closed)
            {
                var error = new ShellWorkflowError(
                    Category: ShellWorkflowFailureCategory.Validation,
                    UserMessage: "Close project failed.",
                    TechnicalDetail: _mainWindowViewModel.ExportStatus,
                    IsRecoverable: true);
                PublishDiagnostic("CloseProject", ShellDiagnosticSeverity.Warning, "Close workflow returned validation failure.");
                return Task.FromResult(ShellWorkflowResult<CloseProjectWorkflowResponse>.Failure(error));
            }

            PublishDiagnostic("CloseProject", ShellDiagnosticSeverity.Info, "Close workflow completed.");
            return Task.FromResult(ShellWorkflowResult<CloseProjectWorkflowResponse>.Success(new CloseProjectWorkflowResponse(Closed: true)));
        }
        finally
        {
            SetState(activeWorkflow: null, isBusy: false);
        }
    }

    private void MainWindowViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(MainWindowViewModel.WindowTitle), StringComparison.Ordinal)
            && !string.Equals(e.PropertyName, nameof(MainWindowViewModel.ExportStatus), StringComparison.Ordinal)
            && !string.Equals(e.PropertyName, nameof(MainWindowViewModel.ProjectSummary), StringComparison.Ordinal))
        {
            return;
        }

        SetState(activeWorkflow: _currentState.ActiveWorkflow, isBusy: _currentState.IsBusy);
    }

    private ShellStateSnapshot BuildState(string? activeWorkflow, bool isBusy)
    {
        return new ShellStateSnapshot(
            WindowTitle: _mainWindowViewModel.WindowTitle,
            StatusMessage: _mainWindowViewModel.ExportStatus,
            ProjectSummary: _mainWindowViewModel.ProjectSummary,
            IsBusy: isBusy,
            ActiveWorkflow: activeWorkflow,
            CapturedAtUtc: DateTimeOffset.UtcNow);
    }

    private void SetState(string? activeWorkflow, bool isBusy)
    {
        _currentState = BuildState(activeWorkflow, isBusy);
        StateChanged?.Invoke(this, _currentState);
    }

    private void PublishDiagnostic(string source, ShellDiagnosticSeverity severity, string message)
    {
        _diagnosticsSink.Publish(new ShellDiagnosticMessage(
            TimestampUtc: DateTimeOffset.UtcNow,
            Severity: severity,
            Source: source,
            Message: message));
    }
}