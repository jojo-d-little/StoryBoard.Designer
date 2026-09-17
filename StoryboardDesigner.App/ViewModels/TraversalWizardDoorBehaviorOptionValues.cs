using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public static class TraversalWizardDoorBehaviorOptionValues
{
    public static IReadOnlyList<TraversalWizardDoorBehaviorOption> All { get; } = Enum.GetValues<TraversalWizardDoorBehaviorOption>();
}
