using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GamePropertyValueRestrictionOption
{
    public GamePropertyValueRestrictionOption(GamePropertyValueRestriction value, string label)
    {
        Value = value;
        Label = label;
    }

    public GamePropertyValueRestriction Value { get; }
    public string Label { get; }
}
