namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record SaveProjectAsWorkflowRequest(
    string BaseFolder,
    string ProjectName);