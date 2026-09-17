using StoryboardDesigner.App.Models;
using System.Text;

namespace StoryboardDesigner.App.Services;

public static class TraversalWizardApplyUtility
{
    private const double NominalDoorPlacementWidth = 96;
    private const double NominalDoorPlacementHeight = 96;

    public static string BuildDoorBaseName(Direction direction)
    {
        return BuildDoorBaseName(ToDirection10(direction));
    }

    public static string BuildDoorBaseName(Direction10 direction)
    {
        return $"Door_{GetDirectionCode(direction)}";
    }

    public static string BuildDoorProducerNotes(Direction direction, string sourceRoomName, string destinationRoomName)
    {
        return BuildDoorProducerNotes(ToDirection10(direction), sourceRoomName, destinationRoomName);
    }

    public static string BuildDoorProducerNotes(Direction10 direction, string sourceRoomName, string destinationRoomName)
    {
        return $"Door opens {direction} from {sourceRoomName} to {destinationRoomName}.";
    }

    public static string BuildTraversalLookEchoMessage(string destinationRoomName)
    {
        return $"There looks to be a {destinationRoomName} thru the door";
    }

    public static string BuildTraversalSharedNameTogether(string roomAName, string roomBName)
    {
        return $"travel_between_{NormalizeNameSegment(roomAName)}_{NormalizeNameSegment(roomBName)}";
    }

    public static string BuildTraversalSharedNameDirectional(string fromRoomName, string toRoomName)
    {
        return $"travel_from_{NormalizeNameSegment(fromRoomName)}_to_{NormalizeNameSegment(toRoomName)}";
    }

    public static string BuildUniqueDoorName(IEnumerable<GameObject> scopedObjects, Direction direction)
    {
        return BuildUniqueDoorName(scopedObjects, ToDirection10(direction));
    }

    public static string BuildUniqueDoorName(IEnumerable<GameObject> scopedObjects, Direction10 direction)
    {
        var baseName = BuildDoorBaseName(direction);
        var taken = scopedObjects
            .Select(obj => obj.Name?.Trim() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        var suffix = 2;
        while (taken.Contains($"{baseName}_{suffix}"))
        {
            suffix++;
        }

        return $"{baseName}_{suffix}";
    }

    public static Direction OppositeDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.NorthEast => Direction.SouthWest,
            Direction.East => Direction.West,
            Direction.SouthEast => Direction.NorthWest,
            Direction.South => Direction.North,
            Direction.SouthWest => Direction.NorthEast,
            Direction.West => Direction.East,
            Direction.NorthWest => Direction.SouthEast,
            _ => Direction.North
        };
    }

    public static Direction10 OppositeDirection(Direction10 direction)
    {
        return direction switch
        {
            Direction10.North => Direction10.South,
            Direction10.NorthEast => Direction10.SouthWest,
            Direction10.East => Direction10.West,
            Direction10.SouthEast => Direction10.NorthWest,
            Direction10.South => Direction10.North,
            Direction10.SouthWest => Direction10.NorthEast,
            Direction10.West => Direction10.East,
            Direction10.NorthWest => Direction10.SouthEast,
            Direction10.Up => Direction10.Down,
            Direction10.Down => Direction10.Up,
            _ => Direction10.North
        };
    }

    public static double ResolveDoorRotationDegrees(Direction direction)
    {
        return ResolveDoorRotationDegrees(ToDirection10(direction));
    }

    public static double ResolveDoorRotationDegrees(Direction10 direction)
    {
        return direction switch
        {
            Direction10.North => 0,
            Direction10.NorthEast => 45,
            Direction10.East => 90,
            Direction10.SouthEast => 135,
            Direction10.South => 180,
            Direction10.SouthWest => 225,
            Direction10.West => 270,
            Direction10.NorthWest => 315,
            _ => 0
        };
    }

    public static (double X, double Y) ResolveDoorWallCenterPosition(Direction direction, double canvasWidth, double canvasHeight)
    {
        return ResolveDoorWallCenterPosition(ToDirection10(direction), canvasWidth, canvasHeight);
    }

    public static (double X, double Y) ResolveDoorWallCenterPosition(Direction10 direction, double canvasWidth, double canvasHeight)
    {
        return ResolveDoorWallCenterPosition(
            direction,
            canvasWidth,
            canvasHeight,
            NominalDoorPlacementWidth,
            NominalDoorPlacementHeight);
    }

    public static (double X, double Y) ResolveDoorWallCenterPosition(
        Direction direction,
        double canvasWidth,
        double canvasHeight,
        double footprintWidth,
        double footprintHeight)
    {
        return ResolveDoorWallCenterPosition(
            ToDirection10(direction),
            canvasWidth,
            canvasHeight,
            footprintWidth,
            footprintHeight);
    }

    public static (double X, double Y) ResolveDoorWallCenterPosition(
        Direction10 direction,
        double canvasWidth,
        double canvasHeight,
        double footprintWidth,
        double footprintHeight)
    {
        var width = double.IsFinite(canvasWidth) && canvasWidth > 0 ? canvasWidth : 800;
        var height = double.IsFinite(canvasHeight) && canvasHeight > 0 ? canvasHeight : 600;
        var normalizedFootprintWidth = double.IsFinite(footprintWidth) && footprintWidth > 0
            ? footprintWidth
            : NominalDoorPlacementWidth;
        var normalizedFootprintHeight = double.IsFinite(footprintHeight) && footprintHeight > 0
            ? footprintHeight
            : NominalDoorPlacementHeight;
        var centerX = width / 2.0;
        var centerY = height / 2.0;
        var maxX = Math.Max(0, width - normalizedFootprintWidth);
        var maxY = Math.Max(0, height - normalizedFootprintHeight);

        var centeredX = Math.Clamp(centerX - (normalizedFootprintWidth / 2.0), 0, maxX);
        var centeredY = Math.Clamp(centerY - (normalizedFootprintHeight / 2.0), 0, maxY);

        return direction switch
        {
            Direction10.North => (centeredX, 0),
            Direction10.NorthEast => (maxX, 0),
            Direction10.East => (maxX, centeredY),
            Direction10.SouthEast => (maxX, maxY),
            Direction10.South => (centeredX, maxY),
            Direction10.SouthWest => (0, maxY),
            Direction10.West => (0, centeredY),
            Direction10.NorthWest => (0, 0),
            Direction10.Up => (centeredX, centeredY),
            Direction10.Down => (centeredX, centeredY),
            _ => (centeredX, 0)
        };
    }

    public static (double Width, double Height) ResolveRotatedFootprint(double width, double height, double rotationDegrees)
    {
        var normalizedWidth = double.IsFinite(width) && width > 0 ? width : NominalDoorPlacementWidth;
        var normalizedHeight = double.IsFinite(height) && height > 0 ? height : NominalDoorPlacementHeight;

        var radians = (rotationDegrees % 360) * Math.PI / 180.0;
        var absCos = Math.Abs(Math.Cos(radians));
        var absSin = Math.Abs(Math.Sin(radians));

        var rotatedWidth = (normalizedWidth * absCos) + (normalizedHeight * absSin);
        var rotatedHeight = (normalizedWidth * absSin) + (normalizedHeight * absCos);

        return (rotatedWidth, rotatedHeight);
    }

    private static string GetDirectionCode(Direction10 direction)
    {
        return direction switch
        {
            Direction10.North => "N",
            Direction10.NorthEast => "NE",
            Direction10.East => "E",
            Direction10.SouthEast => "SE",
            Direction10.South => "S",
            Direction10.SouthWest => "SW",
            Direction10.West => "W",
            Direction10.NorthWest => "NW",
            Direction10.Up => "U",
            Direction10.Down => "D",
            _ => "N"
        };
    }

    private static Direction10 ToDirection10(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction10.North,
            Direction.NorthEast => Direction10.NorthEast,
            Direction.East => Direction10.East,
            Direction.SouthEast => Direction10.SouthEast,
            Direction.South => Direction10.South,
            Direction.SouthWest => Direction10.SouthWest,
            Direction.West => Direction10.West,
            Direction.NorthWest => Direction10.NorthWest,
            _ => Direction10.North
        };
    }

    private static string NormalizeNameSegment(string? value)
    {
        var input = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return "unnamed";
        }

        var builder = new StringBuilder(input.Length);
        var previousWasSeparator = false;
        foreach (var ch in input)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append('_');
            previousWasSeparator = true;
        }

        var normalized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unnamed" : normalized;
    }
}
