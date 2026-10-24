namespace StoryboardDesigner.App.Models;

public enum StructuralPartPlacementKind
{
    ParentRelative,
    RoomCoordinates
}

/// <summary>Placement of a child object's footprint relative to its parent or to the room.</summary>
public sealed class StructuralPartPlacement
{
    public StructuralPartPlacementKind Mode { get; set; } = StructuralPartPlacementKind.ParentRelative;
    public double? ParentRelativeOffsetX { get; set; } = 0;
    public double? ParentRelativeOffsetY { get; set; } = 0;
    public double? RoomCoordinateX { get; set; }
    public double? RoomCoordinateY { get; set; }
}
