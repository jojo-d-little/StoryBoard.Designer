namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record CloseProjectWorkflowRequest(
    bool SaveIfDirty);