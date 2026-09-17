namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record OpenProjectWorkflowResponse(
    string ProjectFilePath,
    string ProjectName);