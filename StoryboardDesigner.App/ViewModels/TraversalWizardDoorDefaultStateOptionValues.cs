using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public static class TraversalWizardDoorDefaultStateOptionValues
{
    public static IReadOnlyList<TraversalWizardDoorDefaultStateOption> All { get; } = Enum.GetValues<TraversalWizardDoorDefaultStateOption>();
}
