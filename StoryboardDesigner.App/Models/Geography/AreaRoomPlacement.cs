namespace StoryboardDesigner.App.Models;

public sealed class AreaRoomPlacement
{
    public Guid RoomId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public int FloorElevation { get; set; }
}
