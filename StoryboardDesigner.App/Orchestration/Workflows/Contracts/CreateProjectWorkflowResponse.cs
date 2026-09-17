namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record CreateProjectWorkflowResponse(
    string ProjectFilePath,
    string ProjectName);