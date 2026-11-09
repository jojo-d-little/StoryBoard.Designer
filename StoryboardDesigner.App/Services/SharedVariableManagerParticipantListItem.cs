namespace StoryboardDesigner.App.Services;

public sealed class SharedVariableManagerParticipantListItem
{
    public string Endpoint { get; init; } = string.Empty;
    public string OwnerDisplayName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public string VariableName { get; init; } = string.Empty;
    public string? Leg { get; init; }
}
