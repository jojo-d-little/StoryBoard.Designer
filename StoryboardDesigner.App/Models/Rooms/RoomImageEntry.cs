namespace StoryboardDesigner.App.Models;

public sealed class RoomImageEntry
{
    public RoomImageSlot Slot { get; set; }
    public int OverlayRenderOrder { get; set; }
    public double OverlayOffsetX { get; set; }
    public double OverlayOffsetY { get; set; }
    public double OverlayRotationDegrees { get; set; }
    public RoomImageVariant Image { get; set; } = new();

    public static int GetDefaultOverlayRenderOrder(RoomImageSlot slot)
    {
        return slot switch
        {
            RoomImageSlot.Down => 0,
            RoomImageSlot.North or RoomImageSlot.East or RoomImageSlot.South or RoomImageSlot.West => 100,
            RoomImageSlot.NorthEast or RoomImageSlot.NorthWest or RoomImageSlot.SouthEast or RoomImageSlot.SouthWest => 200,
            _ => 150
        };
    }
}
