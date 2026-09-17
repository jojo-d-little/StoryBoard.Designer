using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public static class GamePropertyValueRestrictionOptions
{
    public static IReadOnlyList<GamePropertyValueRestrictionOption> All { get; } =
    [
        new(GamePropertyValueRestriction.Unrestricted, "Unrestricted"),
        new(GamePropertyValueRestriction.Numeric, "Numeric"),
        new(GamePropertyValueRestriction.TrueFalse, "True False")
    ];
}
