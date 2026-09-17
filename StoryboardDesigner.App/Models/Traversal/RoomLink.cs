namespace StoryboardDesigner.App.Models;

public sealed class RoomLink
{
    public Guid FromRoomId { get; set; }
    public Guid ToRoomId { get; set; }
    public Direction Direction { get; set; }
    public AreaAdjacencyMode? TraversalModeOverride { get; set; }
}
