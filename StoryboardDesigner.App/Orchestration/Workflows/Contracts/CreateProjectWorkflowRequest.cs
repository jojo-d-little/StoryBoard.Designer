namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record CreateProjectWorkflowRequest(
    string BaseFolder,
    string ProjectName,
    string? StarterProjectFilePath,
    string GameDisplayName,
    string GameSummary,
    List<string> GamePreviewImages);