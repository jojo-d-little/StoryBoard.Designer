using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views.Controls;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalDirectionGatingTests
{
    [Theory]
    [InlineData("RU", "Up")]
    [InlineData("TU", "Up")]
    [InlineData("RD", "Down")]
    [InlineData("TD", "Down")]
    [InlineData("X", "None")]
    [InlineData("", "None")]
    public void VerticalIndicatorCode_MapsToExpectedSeedPreference(string code, string expectedPreference)
    {
        var preferenceType = typeof(AreaRoomPlacementCard).GetNestedType("VerticalSeedPreference", BindingFlags.NonPublic);
        Assert.NotNull(preferenceType);

        var method = typeof(AreaRoomPlacementCard).GetMethod(
            "ResolveVerticalSeedPreferenceFromIndicatorCode",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object?[] { code });
        Assert.Equal(expectedPreference, result!.ToString());
    }

    [Theory]
    [InlineData("None", 8, false, false)]
    [InlineData("Up", 2, true, true)]
    [InlineData("Down", 2, true, true)]
    public void SeedPreference_MapsToExpectedAllowedDirectionSet(
        string preferenceName,
        int expectedCount,
        bool expectUp,
        bool expectDown)
    {
        var preferenceType = typeof(AreaRoomPlacementCard).GetNestedType("VerticalSeedPreference", BindingFlags.NonPublic);
        Assert.NotNull(preferenceType);

        var resolveMethod = typeof(AreaRoomPlacementCard).GetMethod(
            "ResolveAllowedTraversalDirections",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(resolveMethod);

        var preference = Enum.Parse(preferenceType!, preferenceName);
        var result = resolveMethod!.Invoke(null, new[] { preference });
        var allowed = Assert.IsAssignableFrom<IReadOnlyCollection<Direction10>>(result);

        Assert.Equal(expectedCount, allowed.Count);
        Assert.Equal(expectUp, allowed.Contains(Direction10.Up));
        Assert.Equal(expectDown, allowed.Contains(Direction10.Down));
    }

    [Fact]
    public void BuildAddTraversalSeed_WithVerticalUpPreference_SeedsUpDirectionAndUpperRoom()
    {
        var source = new Room { Name = "Source" };
        var upper = new Room { Name = "Upper" };
        var east = new Room { Name = "East" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { source, east, upper },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = source.Id, X = 24, Y = 24, FloorElevation = 0 },
                new() { RoomId = upper.Id, X = 24, Y = 24, FloorElevation = 1 },
                new() { RoomId = east.Id, X = 204, Y = 24, FloorElevation = 0 }
            }
        };

        var seed = InvokeBuildAddTraversalSeed(area, source, "Up");

        Assert.Equal(Direction10.Up, seed.BaseTraversalDirectionFromA);
        Assert.Equal(source.Id, seed.RoomAId);
        Assert.Equal(upper.Id, seed.RoomBId);
    }

    [Fact]
    public void BuildAddTraversalSeed_WithVerticalDownPreference_SeedsDownDirectionAndLowerRoom()
    {
        var source = new Room { Name = "Source" };
        var lower = new Room { Name = "Lower" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { source, lower },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = source.Id, X = 24, Y = 24, FloorElevation = 0 },
                new() { RoomId = lower.Id, X = 24, Y = 24, FloorElevation = -1 }
            }
        };

        var seed = InvokeBuildAddTraversalSeed(area, source, "Down");

        Assert.Equal(Direction10.Down, seed.BaseTraversalDirectionFromA);
        Assert.Equal(source.Id, seed.RoomAId);
        Assert.Equal(lower.Id, seed.RoomBId);
    }

    [Fact]
    public void DirectionValues_DefaultTraversalDirections_AreEightWayAndExcludeVertical()
    {
        Assert.Equal(8, DirectionValues.DefaultTraversalDirections.Count);
        Assert.DoesNotContain(Direction10.Up, DirectionValues.DefaultTraversalDirections);
        Assert.DoesNotContain(Direction10.Down, DirectionValues.DefaultTraversalDirections);

        var vertical = DirectionValues.VerticalTraversalDirections.ToList();
        Assert.Equal(2, vertical.Count);
        Assert.Equal(Direction10.Up, vertical[0]);
        Assert.Equal(Direction10.Down, vertical[1]);
    }

    [Theory]
    [InlineData(Direction10.Up, 10, true, true)]
    [InlineData(Direction10.Down, 10, true, true)]
    [InlineData(Direction10.East, 8, false, false)]
    public void AreaRoomPlacementCard_EditAllowedDirections_PreserveVerticalOnlyWhenConnectionIsVertical(
        Direction10 baseDirection,
        int expectedCount,
        bool expectUp,
        bool expectDown)
    {
        var method = typeof(AreaRoomPlacementCard).GetMethod(
            "ResolveAllowedTraversalDirectionsForEdit",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var connection = new TraversalConnection
        {
            RoomAId = Guid.NewGuid(),
            RoomBId = Guid.NewGuid(),
            BaseTraversalDirectionFromA = baseDirection
        };

        var result = method!.Invoke(null, new object?[] { connection });
        var allowed = Assert.IsAssignableFrom<IReadOnlyCollection<Direction10>>(result);

        Assert.Equal(expectedCount, allowed.Count);
        Assert.Equal(expectUp, allowed.Contains(Direction10.Up));
        Assert.Equal(expectDown, allowed.Contains(Direction10.Down));
    }

    [Theory]
    [InlineData(Direction10.Up, 10, true, true)]
    [InlineData(Direction10.Down, 10, true, true)]
    [InlineData(Direction10.West, 8, false, false)]
    public void AreaMapCanvas_EditAllowedDirections_PreserveVerticalOnlyWhenConnectionIsVertical(
        Direction10 baseDirection,
        int expectedCount,
        bool expectUp,
        bool expectDown)
    {
        var method = typeof(AreaMapCanvas).GetMethod(
            "ResolveAllowedTraversalDirectionsForEdit",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var connection = new TraversalConnection
        {
            RoomAId = Guid.NewGuid(),
            RoomBId = Guid.NewGuid(),
            BaseTraversalDirectionFromA = baseDirection
        };

        var result = method!.Invoke(null, new object?[] { connection });
        var allowed = Assert.IsAssignableFrom<IReadOnlyCollection<Direction10>>(result);

        Assert.Equal(expectedCount, allowed.Count);
        Assert.Equal(expectUp, allowed.Contains(Direction10.Up));
        Assert.Equal(expectDown, allowed.Contains(Direction10.Down));
    }

    private static TraversalConnection InvokeBuildAddTraversalSeed(Area area, Room sourceRoom, string preferenceName)
    {
        var preferenceType = typeof(AreaRoomPlacementCard).GetNestedType("VerticalSeedPreference", BindingFlags.NonPublic);
        Assert.NotNull(preferenceType);

        var method = typeof(AreaRoomPlacementCard).GetMethod("BuildAddTraversalSeed", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var preference = Enum.Parse(preferenceType!, preferenceName);
        var seed = method!.Invoke(null, new object?[] { area, sourceRoom, preference });

        return Assert.IsType<TraversalConnection>(seed);
    }
}
