using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.ViewModels;

public static class TraversalAccessModeValues
{
    public static IReadOnlyList<TraversalAccessMode> All { get; } = Enum.GetValues<TraversalAccessMode>();
}
