using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public static class AreaAdjacencyModeValues
{
    public static IReadOnlyList<AreaAdjacencyMode> All { get; } = Enum.GetValues<AreaAdjacencyMode>();
}
