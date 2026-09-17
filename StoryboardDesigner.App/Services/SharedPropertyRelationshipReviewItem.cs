namespace StoryboardDesigner.App.Services;

public sealed class SharedPropertyRelationshipReviewItem
{
    public Guid RelationshipId { get; init; }
    public int Priority { get; init; }
    public string ParticipantKind { get; init; } = string.Empty;
    public Guid ParticipantOwnerId { get; init; }
    public string ParticipantVariableName { get; init; } = string.Empty;
    public string? ParticipantLeg { get; init; }
    public string CounterpartEndpoint { get; init; } = string.Empty;
    public string Directionality { get; init; } = string.Empty;
    public string TransformMode { get; init; } = string.Empty;
    public string EnabledState { get; init; } = string.Empty;
    public string SyncMode { get; init; } = string.Empty;
    public string RuntimeDiagnostics { get; init; } = "No runtime diagnostics yet";
}
