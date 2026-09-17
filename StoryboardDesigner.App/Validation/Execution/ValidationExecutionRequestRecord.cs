using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed record ValidationExecutionRequest(
    ProjectModel Project,
    ValidationExecutionKind ExecutionKind = ValidationExecutionKind.WholeProject,
    ScopeNodeBase? RootScope = null,
    bool IncludeDescendants = true,
    ValidationCompletionMode CompletionMode = ValidationCompletionMode.FullReport,
    string? ProjectFilePath = null);
