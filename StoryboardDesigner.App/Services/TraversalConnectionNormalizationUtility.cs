using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class TraversalConnectionNormalizationUtility
{
    public static List<TraversalConnection> NormalizeLinks(IEnumerable<RoomLink> links)
    {
        var normalized = new List<TraversalConnection>();

        var validLinks = links
            .Where(link => link.FromRoomId != Guid.Empty && link.ToRoomId != Guid.Empty && link.FromRoomId != link.ToRoomId)
            .ToList();

        foreach (var group in validLinks
                     .GroupBy(link => ToPairKey(link.FromRoomId, link.ToRoomId))
                     .OrderBy(group => group.Key.A)
                     .ThenBy(group => group.Key.B))
        {
            var roomAId = group.Key.A;
            var roomBId = group.Key.B;

            var linkAtoB = group
                .Where(link => link.FromRoomId == roomAId && link.ToRoomId == roomBId)
                .OrderBy(link => link.Direction.ToString(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            var linkBtoA = group
                .Where(link => link.FromRoomId == roomBId && link.ToRoomId == roomAId)
                .OrderBy(link => link.Direction.ToString(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (linkAtoB is null && linkBtoA is null)
            {
                continue;
            }

            var traversalAccessMode = linkAtoB is not null && linkBtoA is not null
                ? TraversalAccessMode.TwoWay
                : linkAtoB is not null
                    ? TraversalAccessMode.OneWayAtoB
                    : TraversalAccessMode.OneWayBtoA;

            var baseTraversalDirectionFromA = linkAtoB is not null
                ? ToDirection10(linkAtoB.Direction)
                : ToDirection10(Opposite(linkBtoA!.Direction));

            normalized.Add(new TraversalConnection
            {
                RoomAId = roomAId,
                RoomBId = roomBId,
                BaseTraversalDirectionFromA = baseTraversalDirectionFromA,
                TraversalModeOverride = linkAtoB?.TraversalModeOverride ?? linkBtoA?.TraversalModeOverride,
                TraversalAccessMode = traversalAccessMode
            });
        }

        return normalized;
    }

    private static (Guid A, Guid B) ToPairKey(Guid first, Guid second)
    {
        return first.CompareTo(second) <= 0 ? (first, second) : (second, first);
    }

    private static Direction Opposite(Direction direction)
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
}
