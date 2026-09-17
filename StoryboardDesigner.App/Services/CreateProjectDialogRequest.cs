namespace StoryboardDesigner.App.Services;

public sealed class CreateProjectDialogRequest
{
    public required string DefaultProjectFolder { get; init; }

    public string DefaultGameDisplayName { get; init; } = string.Empty;

    public string DefaultGameSummary { get; init; } = string.Empty;

    public List<string> DefaultGamePreviewImages { get; init; } = new();

    public required string StarterProjectsRoot { get; init; }

    public required string DefaultStarterBrowseFolder { get; init; }

    public required IReadOnlyList<StarterProjectOption> BuiltInStarterProjects { get; init; }
}
