namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record ShellWorkflowError(
    ShellWorkflowFailureCategory Category,
    string UserMessage,
    string? TechnicalDetail,
    bool IsRecoverable);