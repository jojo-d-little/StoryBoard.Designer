using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class TraversalModeResolutionUtility
{
    public static AreaAdjacencyMode ResolveEffectiveTraversalMode(
        ProjectModel project,
        Area area,
        Room? room = null,
        RoomLink? traversalLeg = null)
    {
        if (traversalLeg?.TraversalModeOverride is { } traversalLegOverride)
        {
            return traversalLegOverride;
        }

        if (room?.TraversalModeOverride is { } roomOverride)
        {
            return roomOverride;
        }

        if (area.TraversalModeOverride is { } areaOverride)
        {
            return areaOverride;
        }

        // Preserve existing area-level behavior during transition.
        return area.AdjacencyMode;
    }
}
