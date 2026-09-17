using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.ViewModels;

public static class OpenablePolicyValues
{
    public static IReadOnlyList<OpenablePolicy> All { get; } = Enum.GetValues<OpenablePolicy>();
}
