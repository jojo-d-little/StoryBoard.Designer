using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public static class ChildCommandForwardingModeValues
{
    public static IReadOnlyList<ChildCommandForwardingMode> Selectable { get; } =
    [
        ChildCommandForwardingMode.ChildrenAfterParent,
        ChildCommandForwardingMode.ChildrenBeforeParent
    ];
}
