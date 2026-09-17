using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public static class DirectionValues
{
    public static IReadOnlyList<Direction> All { get; } = Enum.GetValues<Direction>();
    public static IReadOnlyList<Direction10> All10 { get; } = Enum.GetValues<Direction10>();

    public static IReadOnlyList<Direction10> DefaultTraversalDirections { get; } = new[]
    {
        Direction10.North,
        Direction10.NorthEast,
        Direction10.East,
        Direction10.SouthEast,
        Direction10.South,
        Direction10.SouthWest,
        Direction10.West,
        Direction10.NorthWest
    };

    public static IReadOnlyList<Direction10> VerticalTraversalDirections { get; } = new[]
    {
        Direction10.Up,
        Direction10.Down
    };
}
