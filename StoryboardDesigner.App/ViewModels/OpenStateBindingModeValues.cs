using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.ViewModels;

public static class OpenStateBindingModeValues
{
    public static IReadOnlyList<OpenStateBindingMode> All { get; } = Enum.GetValues<OpenStateBindingMode>();
}
