using Storyboard.Shared.GameServices.Actions;

namespace StoryboardDesigner.App.Services;

public static class ActionEchoEditorEntryBuilder
{
    public static IReadOnlyList<ActionEchoEditorEntry> BuildEntries(
        CommandActionType actionType,
        IReadOnlyDictionary<string, string>? sourceMap)
    {
        var normalizedSource = NormalizeSourceMap(actionType, sourceMap);
        var supported = RuntimeCommandActionExecutor.GetSupportedResultCodes(actionType);

        var entries = new List<ActionEchoEditorEntry>();
        foreach (var descriptor in supported)
        {
            entries.Add(new ActionEchoEditorEntry
            {
                Token = descriptor.Token,
                IsSupported = true,
                OutcomeLabel = descriptor.Outcome.ToString(),
                Script = normalizedSource.TryGetValue(descriptor.Token, out var script) ? script : string.Empty
            });
        }

        foreach (var (token, script) in normalizedSource.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(actionType, token, out _))
            {
                continue;
            }

            entries.Add(new ActionEchoEditorEntry
            {
                Token = token,
                IsSupported = false,
                OutcomeLabel = "Unsupported",
                Script = script
            });
        }

        return entries;
    }

    public static IReadOnlyDictionary<string, string> ToOutcomeMessageMap(IEnumerable<ActionEchoEditorEntry> entries)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var token = entry.Token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            map[token] = entry.Script ?? string.Empty;
        }

        return map;
    }

    private static Dictionary<string, string> NormalizeSourceMap(
        CommandActionType actionType,
        IReadOnlyDictionary<string, string>? source)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return normalized;
        }

        foreach (var (key, value) in source)
        {
            var token = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(actionType, token, out var descriptor))
            {
                normalized[descriptor.Token] = value ?? string.Empty;
                continue;
            }

            normalized[token] = value ?? string.Empty;
        }

        return normalized;
    }
}
