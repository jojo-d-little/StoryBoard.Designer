namespace StoryboardDesigner.App.Services;

public sealed class SharedVariableManagerListItem
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string DefaultValue { get; init; } = string.Empty;
    public string ValueRestriction { get; init; } = string.Empty;
    public int ParticipantCount { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<SharedVariableManagerParticipantListItem> Participants { get; init; } = Array.Empty<SharedVariableManagerParticipantListItem>();
}

public sealed class SharedVariableManagerParticipantListItem
{
    public string Endpoint { get; init; } = string.Empty;
    public string OwnerDisplayName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public string VariableName { get; init; } = string.Empty;
    public string? Leg { get; init; }
}
