using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GamePropertyChoiceItem
{
    public required string Value { get; init; }
    public required string ScopePath { get; init; }
    public required string OwnerVariable { get; init; }
    public string DisplayName => OwnerVariable;
    public required PropertyResolutionScope Scope { get; init; }
    public required GamePropertyValueRestriction ValueRestriction { get; init; }
    public int Priority { get; init; }
    public GamePropertyChoiceRelation Relation { get; init; } = GamePropertyChoiceRelation.Other;
}
