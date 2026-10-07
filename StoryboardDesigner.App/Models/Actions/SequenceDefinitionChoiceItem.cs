namespace StoryboardDesigner.App.Models;

public sealed class SequenceDefinitionChoiceItem
{
    public required Guid SequenceId { get; init; }
    public required string DisplayName { get; init; }
    public required string ScopeName { get; init; }
    public string Label => $"{DisplayName} ({ScopeName})";
}
