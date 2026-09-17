using System.Text.Json;
using System.IO;
using StoryboardDesigner.App.Models;
using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Validation.Rules.Project;

internal static class EventSubscriptionRuleSupport
{
    private const string EventPayloadManifestRelativePath = "Config\\event-payload.manifest.json";

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
        var manifestPath = ResolveManifestPath(EventPayloadManifestRelativePath);
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            if (!root.TryGetProperty("events", out var eventsElement)
                || eventsElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var eventElement in eventsElement.EnumerateArray())
            {
                var eventKey = ReadTrimmedString(eventElement, "eventKey");
                if (string.IsNullOrWhiteSpace(eventKey))
                {
                    continue;
                }

                eventKeys.Add(eventKey);
                var payloadKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (eventElement.TryGetProperty("payloadMappings", out var mappingsElement)
                    && mappingsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var mapping in mappingsElement.EnumerateArray())
                    {
                        var payloadKey = ReadTrimmedString(mapping, "payloadKey");
                        if (!string.IsNullOrWhiteSpace(payloadKey))
                        {
                            payloadKeys.Add(payloadKey);
                        }
                    }
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
            var manifest = new RuntimeSessionAnchorManifestReader().Load();
            foreach (var anchor in manifest.Anchors)
            {
                var anchorKey = anchor.AnchorKey?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(anchorKey))
                {
                    continue;
                }

                anchorKeys.Add(anchorKey);
                var subProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var subProperty in anchor.SupportedSubProperties)
                {
                    var propertyKey = subProperty.PropertyKey?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(propertyKey))
                    {
                        continue;
                    }

                    subProperties.Add(propertyKey);
                    allSubPropertyKeys.Add(propertyKey);
                }

                subPropertiesByAnchor[anchorKey] = subProperties;
            }
        }
        catch
        {
            // Keep validation resilient when manifests are unavailable or malformed.
        }
    }

    private static string ResolveManifestPath(string relativeManifestPath)
    {
        var defaultPath = Path.Combine(AppContext.BaseDirectory, relativeManifestPath);
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StoryboardDesigner.slnx")))
            {
                var repoPath = Path.Combine(current.FullName, "Storyboard.GameEngine", relativeManifestPath);
                return repoPath;
            }

            current = current.Parent;
        }

        return defaultPath;
    }

    private static string ReadTrimmedString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return value.GetString()?.Trim() ?? string.Empty;
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
