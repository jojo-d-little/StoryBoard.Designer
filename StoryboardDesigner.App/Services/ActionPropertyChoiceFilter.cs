using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Services;

/// <summary>Applies the common eligibility rules for action property choices.</summary>
public static class ActionPropertyChoiceFilter
{
    public static IReadOnlyList<GamePropertyChoiceItem> Filter(
        IEnumerable<GamePropertyChoiceItem> choices,
        bool booleanOnly = false,
        bool requireWritable = false) =>
        choices.Where(choice => (!booleanOnly || choice.ValueRestriction == GamePropertyValueRestriction.TrueFalse)
            && (!requireWritable || choice.CanWrite)).ToList();
}
