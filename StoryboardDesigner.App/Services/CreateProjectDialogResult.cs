namespace StoryboardDesigner.App.Services;

public sealed class CreateProjectDialogResult
{
    public required string ProjectFolder { get; init; }

    public required string ProjectName { get; init; }

    public string GameDisplayName { get; init; } = string.Empty;

    public string GameSummary { get; init; } = string.Empty;

    public List<string> GamePreviewImages { get; init; } = new();

    public string? StarterProjectFilePath { get; init; }

    public required string LastStarterBrowseFolder { get; init; }
}
