using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopeObjectChoiceItem
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string ScopePath { get; init; }
    public required string OwnerContext { get; init; }
    public required PropertyResolutionScope Scope { get; init; }
    public IReadOnlyList<GamePropertyChoiceRelation> Relations { get; init; } = new List<GamePropertyChoiceRelation> { GamePropertyChoiceRelation.Other };
}
