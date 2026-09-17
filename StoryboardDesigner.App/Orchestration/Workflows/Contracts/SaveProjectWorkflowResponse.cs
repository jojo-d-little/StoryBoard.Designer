namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record SaveProjectWorkflowResponse(
    string? ProjectFilePath,
    bool WasDirtyBeforeSave);