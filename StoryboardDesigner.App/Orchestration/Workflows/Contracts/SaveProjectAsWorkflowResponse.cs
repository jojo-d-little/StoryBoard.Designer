namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record SaveProjectAsWorkflowResponse(
    string ProjectFilePath,
    string ProjectName);