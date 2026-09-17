namespace StoryboardDesigner.App.Services;

public sealed class SharedVariableManagerListItem
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public int ParticipantCount { get; init; }
    public string Status { get; init; } = string.Empty;
}
