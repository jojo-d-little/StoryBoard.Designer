namespace StoryboardDesigner.App.Models;

public sealed class MaterializeSourceObjectChoiceItem
{
    public Guid ObjectId { get; init; }

    public GameObject? SourceObject { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string ScopePath { get; init; } = string.Empty;

    public string SourceCategory { get; init; } = string.Empty;
}