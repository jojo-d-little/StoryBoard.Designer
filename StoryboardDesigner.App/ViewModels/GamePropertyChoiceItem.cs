using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GamePropertyChoiceItem
{
    public required string Value { get; init; }
    public required string ScopePath { get; init; }
    public required string OwnerVariable { get; init; }
    public string DisplayName => OwnerVariable;
    public string OwnerLabel
    {
        get
        {
            var ownerVariable = OwnerVariable?.Trim() ?? string.Empty;
            var propertyName = PropertyName?.Trim() ?? string.Empty;
            var suffix = string.IsNullOrWhiteSpace(propertyName) ? string.Empty : "." + propertyName;
            var separator = !string.IsNullOrEmpty(suffix)
                && ownerVariable.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? ownerVariable.Length - suffix.Length
                    : ownerVariable.LastIndexOf('.');

            var owner = separator > 0 ? ownerVariable[..separator].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(owner)
                || string.Equals(owner, "self", StringComparison.OrdinalIgnoreCase)
                || string.Equals(owner, "owner", StringComparison.OrdinalIgnoreCase)
                || string.Equals(owner, "traversal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(owner, "traversal-leg", StringComparison.OrdinalIgnoreCase))
            {
                return ScopePath?.Trim() ?? string.Empty;
            }

            var scopePath = ScopePath?.Trim() ?? string.Empty;
            var finalScopeSegment = scopePath
                .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();
            return string.Equals(finalScopeSegment, owner, StringComparison.OrdinalIgnoreCase)
                ? scopePath
                : string.IsNullOrWhiteSpace(scopePath) ? owner : $"{scopePath} · {owner}";
        }
    }
    public required PropertyResolutionScope Scope { get; init; }
    public required GamePropertyValueRestriction ValueRestriction { get; init; }
    public int Priority { get; init; }
    public GamePropertyChoiceRelation Relation { get; init; } = GamePropertyChoiceRelation.Other;
    public Guid? OwnerId { get; init; }
    public string? PropertyName { get; init; }
    public IReadOnlyList<string> EligibleAnchors { get; init; } = Array.Empty<string>();
}
