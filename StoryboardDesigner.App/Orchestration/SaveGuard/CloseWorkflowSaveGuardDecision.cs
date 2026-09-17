namespace StoryboardDesigner.App.Orchestration.SaveGuard;

public sealed record CloseWorkflowSaveGuardDecision(
    CloseWorkflowSaveGuardDecisionKind Kind,
    string? UserMessage);