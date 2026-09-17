using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class TraversalLegNodeViewModel : HierarchyNodeViewModel
{
    public TraversalLegNodeViewModel(
        TraversalConnection connection,
        RoomNodeViewModel roomNode,
        bool isFromRoomA,
        Direction10 direction,
        Guid destinationRoomId,
        string destinationRoomName,
        RoomTraversalLegsNodeViewModel parent)
        : base(direction.ToString(), parent)
    {
        Connection = connection;
        RoomNode = roomNode;
        IsFromRoomA = isFromRoomA;
        Direction = direction;
        DestinationRoomId = destinationRoomId;
        DestinationRoomName = destinationRoomName;
        NodeTypeLabel = "Traversal Leg";
    }

    public TraversalConnection Connection { get; }
    public RoomNodeViewModel RoomNode { get; }
    public bool IsFromRoomA { get; }
    public Direction10 Direction { get; }
    public Guid DestinationRoomId { get; }
    public string DestinationRoomName { get; }
    public string DestinationToolTip => string.IsNullOrWhiteSpace(DestinationRoomName)
        ? "Destination room unavailable"
        : $"Destination: {DestinationRoomName}";

    public TraversalLegState LegState => IsFromRoomA
        ? Connection.TraversalStateFromA
        : Connection.TraversalStateFromB;

    protected override void RenameModel(string newName)
    {
    }
}
