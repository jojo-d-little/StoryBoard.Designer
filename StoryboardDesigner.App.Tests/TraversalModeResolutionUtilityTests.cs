using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public class TraversalModeResolutionUtilityTests
{
    [Fact]
    public void ResolveEffectiveTraversalMode_UsesExpectedPrecedence()
    {
        var project = new ProjectModel
        {
            DefaultTraversalMode = AreaAdjacencyMode.FourDirectional
        };

        var area = new Area
        {
            Name = "Area",
            AdjacencyMode = AreaAdjacencyMode.FourDirectional,
            TraversalModeOverride = AreaAdjacencyMode.EightDirectional
        };

        var room = new Room
        {
            Name = "Room",
            TraversalModeOverride = AreaAdjacencyMode.FourDirectional
        };

        var leg = new RoomLink
        {
            FromRoomId = Guid.NewGuid(),
            ToRoomId = Guid.NewGuid(),
            Direction = Direction.North,
            TraversalModeOverride = AreaAdjacencyMode.EightDirectional
        };

        var resolvedFromLeg = TraversalModeResolutionUtility.ResolveEffectiveTraversalMode(project, area, room, leg);
        Assert.Equal(AreaAdjacencyMode.EightDirectional, resolvedFromLeg);

        leg.TraversalModeOverride = null;
        var resolvedFromRoom = TraversalModeResolutionUtility.ResolveEffectiveTraversalMode(project, area, room, leg);
        Assert.Equal(AreaAdjacencyMode.FourDirectional, resolvedFromRoom);

        room.TraversalModeOverride = null;
        var resolvedFromArea = TraversalModeResolutionUtility.ResolveEffectiveTraversalMode(project, area, room, leg);
        Assert.Equal(AreaAdjacencyMode.EightDirectional, resolvedFromArea);

        area.TraversalModeOverride = null;
        var resolvedFromLegacyArea = TraversalModeResolutionUtility.ResolveEffectiveTraversalMode(project, area, room, leg);
        Assert.Equal(AreaAdjacencyMode.FourDirectional, resolvedFromLegacyArea);
    }
}
