namespace StoryboardDesigner.App.Models;

public sealed class TraversalConnection
{
    public Guid TraversalConnectionId { get; set; } = Guid.NewGuid();
    public Guid RoomAId { get; set; }
    public Guid RoomBId { get; set; }
    public Direction10 BaseTraversalDirectionFromA { get; set; } = Direction10.North;
    public AreaAdjacencyMode? TraversalModeOverride { get; set; }
    public TraversalAccessMode TraversalAccessMode { get; set; } = TraversalAccessMode.TwoWay;
    public string? PresentationEffectKey { get; set; }
    public TraversalLegState TraversalStateFromA { get; set; } = new();
    public TraversalLegState TraversalStateFromB { get; set; } = new();
    public OpenStateBindingMode OpenStateBindingMode { get; set; } = OpenStateBindingMode.Independent;
    public List<GamePropertyDefinition> Variables { get; set; } = new();
}
