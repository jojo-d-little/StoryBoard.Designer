using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed class EventSubscriptionActionNameSuggestionDiscoveryService : IEventSubscriptionActionNameSuggestionDiscoveryService
{
    public IReadOnlyList<string> DiscoverLikelyActionNames(ScopeNodeBase? scopeNode)
    {
        if (scopeNode is null)
        {
            return Array.Empty<string>();
        }

        var ranked = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var scopeDistance = 0;

        foreach (var node in scopeNode.EnumerateSelfAndAncestors())
        {
            foreach (var action in EnumerateActions(node))
            {
                var normalized = action.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!ranked.TryAdd(normalized, scopeDistance) && ranked[normalized] > scopeDistance)
                {
                    ranked[normalized] = scopeDistance;
                }
            }

            scopeDistance++;
        }

        return ranked
            .OrderBy(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => pair.Key)
            .ToList();
    }

    private static IEnumerable<CommandAction> EnumerateActions(IScopedAwareNode node)
    {
        return node switch
        {
            ProjectModel project => project.GlobalScope.AvailableActions,
            Planet planet => planet.AvailableActions,
            Country country => country.AvailableActions,
            Area area => area.AvailableActions,
            Room room => room.AvailableActions,
            GameObject gameObject => gameObject.AvailableActions,
            _ => Array.Empty<CommandAction>()
        };
    }
}
