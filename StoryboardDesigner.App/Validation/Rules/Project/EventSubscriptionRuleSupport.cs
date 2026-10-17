using StoryboardDesigner.App.Models;
using Storyboard.GameEngine.Configuration.Catalogs;

namespace StoryboardDesigner.App.Validation.Rules.Project;

internal static class EventSubscriptionRuleSupport
{
    public static string BuildScopePath(ScopeNodeBase node)
    {
        var scopes = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name));

        return string.Join(" / ", scopes);
    }

    public static IEnumerable<(EventSubscriptionDefinition Subscription, int Index, string SubscriptionPath)> EnumerateSubscriptions(ScopeNodeBase scope)
    {
        var subscriptions = scope.EventSubscriptions ?? new List<EventSubscriptionDefinition>();
        var scopePath = BuildScopePath(scope);

        for (var subscriptionIndex = 0; subscriptionIndex < subscriptions.Count; subscriptionIndex++)
        {
            var subscription = subscriptions[subscriptionIndex];
            var subscriptionPath = $"{scopePath} / eventSubscription[{subscriptionIndex + 1}]";
            yield return (subscription, subscriptionIndex, subscriptionPath);
        }
    }

    public static EventPayloadManifestIndex LoadManifestIndex()
    {
        var eventKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var payloadKeysByEvent = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var anchorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var subPropertiesByAnchor = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var allSubPropertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        LoadEventManifestData(eventKeys, payloadKeysByEvent);
        LoadSessionAnchorManifestData(anchorKeys, subPropertiesByAnchor, allSubPropertyKeys);

        if (eventKeys.Count == 0
            && payloadKeysByEvent.Count == 0
            && anchorKeys.Count == 0
            && subPropertiesByAnchor.Count == 0
            && allSubPropertyKeys.Count == 0)
        {
            return EventPayloadManifestIndex.Empty;
        }

        return new EventPayloadManifestIndex(
            eventKeys,
            payloadKeysByEvent,
            anchorKeys,
            subPropertiesByAnchor,
            allSubPropertyKeys);
    }

    private static void LoadEventManifestData(
        ISet<string> eventKeys,
        IDictionary<string, HashSet<string>> payloadKeysByEvent)
    {
        try
        {
            var manifest = EventPayloadManifestLoader.Load();
            foreach (var eventDefinition in manifest.Events)
            {
                var eventKey = eventDefinition.EventKey?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(eventKey))
                {
                    continue;
                }

                eventKeys.Add(eventKey);
                var payloadKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var argument in eventDefinition.PayloadArguments)
                {
                    var payloadKey = argument.PayloadKey?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(payloadKey)) payloadKeys.Add(payloadKey);
                }

                payloadKeysByEvent[eventKey] = payloadKeys;
            }
        }
        catch
        {
            // Keep validation resilient when manifests are unavailable or malformed.
        }
    }

    private static void LoadSessionAnchorManifestData(
        ISet<string> anchorKeys,
        IDictionary<string, HashSet<string>> subPropertiesByAnchor,
        ISet<string> allSubPropertyKeys)
    {
        try
        {
            var manifest = SessionAnchorManifestLoader.Load();
            foreach (var anchor in manifest.Anchors)
            {
                var anchorKey = anchor.AnchorKey?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(anchorKey))
                {
                    continue;
                }

                anchorKeys.Add(anchorKey);
                subPropertiesByAnchor[anchorKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Keep validation resilient when manifests are unavailable or malformed.
        }
    }

    internal sealed class EventPayloadManifestIndex
    {
        public static EventPayloadManifestIndex Empty { get; } = new(
            eventKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            payloadKeysByEvent: new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
            anchorKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            subPropertiesByAnchor: new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
            allSubPropertyKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public EventPayloadManifestIndex(
            HashSet<string> eventKeys,
            Dictionary<string, HashSet<string>> payloadKeysByEvent,
            HashSet<string> anchorKeys,
            Dictionary<string, HashSet<string>> subPropertiesByAnchor,
            HashSet<string> allSubPropertyKeys)
        {
            EventKeys = eventKeys;
            PayloadKeysByEvent = payloadKeysByEvent;
            AnchorKeys = anchorKeys;
            SubPropertiesByAnchor = subPropertiesByAnchor;
            AllSubPropertyKeys = allSubPropertyKeys;
        }

        public HashSet<string> EventKeys { get; }

        public Dictionary<string, HashSet<string>> PayloadKeysByEvent { get; }

        public HashSet<string> AnchorKeys { get; }

        public Dictionary<string, HashSet<string>> SubPropertiesByAnchor { get; }

        public HashSet<string> AllSubPropertyKeys { get; }
    }
}
