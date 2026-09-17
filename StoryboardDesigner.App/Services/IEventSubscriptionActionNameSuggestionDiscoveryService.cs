using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Discovers likely action-name suggestions for event subscription bindings.
/// </summary>
public interface IEventSubscriptionActionNameSuggestionDiscoveryService
{
    /// <summary>
    /// Returns action names ranked by likely runtime resolution order for the provided scope.
    /// </summary>
    /// <param name="scopeNode">The scope where the event subscription is authored.</param>
    /// <returns>A deterministic list of candidate action names, ordered by nearest scope first.</returns>
    IReadOnlyList<string> DiscoverLikelyActionNames(ScopeNodeBase? scopeNode);
}
