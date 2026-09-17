namespace StoryboardDesigner.App.Services;

public sealed class PreviewItem
{
    public required string ScopePath { get; init; }
    public required string Channel { get; init; }
    public required string CurrentPath { get; init; }
    public required string Status { get; init; }
    public string? ProposedPath { get; init; }
    public Action? ApplyAction { get; init; }
}
