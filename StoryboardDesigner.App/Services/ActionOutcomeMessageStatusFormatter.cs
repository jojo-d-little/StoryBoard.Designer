using Storyboard.Shared.GameServices.Actions;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class ActionOutcomeMessageStatusFormatter
{
    public static IReadOnlyList<ActionOutcomeMessageStatus> BuildStatuses(CommandAction action)
    {
        var supportedCodes = RuntimeCommandActionExecutor.GetSupportedResultCodes(action.ActionType);
        if (supportedCodes.Count == 0)
        {
            return Array.Empty<ActionOutcomeMessageStatus>();
        }

        var effectiveMap = BuildEffectiveOutcomeMessageMap(action);
        return supportedCodes
            .Select(code => new ActionOutcomeMessageStatus(
                code.Token,
                effectiveMap.TryGetValue(code.Token, out var script) && !string.IsNullOrWhiteSpace(script),
                effectiveMap.TryGetValue(code.Token, out var resolvedScript) ? resolvedScript : string.Empty))
            .ToList();
    }

    public static string BuildCompactStatusText(CommandAction action)
    {
        var statuses = BuildStatuses(action);
        if (statuses.Count == 0)
        {
            return ActionOutcomeMessageTextFormatter.BuildNoResultCodesText();
        }

        var definedCount = statuses.Count(static status => status.IsDefined);
        return ActionOutcomeMessageTextFormatter.BuildCompactStatusText(definedCount, statuses.Count);
    }

    public static string BuildTooltipText(CommandAction action)
    {
        var statuses = BuildStatuses(action);
        if (statuses.Count == 0)
        {
            return "No result codes are registered for this action type.";
        }

        var lines = statuses
            .Select(status => $"{status.Token}: {(status.IsDefined ? "defined" : "undefined")}")
            .ToList();

        var unsupportedCount = CountUnsupportedOutcomeEntries(action);
        if (unsupportedCount > 0)
        {
            lines.Add(UnsupportedOutcomeEntryWarningTextFormatter.BuildTooltipSummaryLine(unsupportedCount));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static int CountUnsupportedOutcomeEntries(CommandAction action)
    {
        var normalized = NormalizeOutcomeMessageMap(action.ActionType, action.OutcomeMessageMap);
        var count = 0;
        foreach (var token in normalized.Keys)
        {
            if (!RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(action.ActionType, token, out _))
            {
                count++;
            }
        }

        return count;
    }

    private static Dictionary<string, string> BuildEffectiveOutcomeMessageMap(CommandAction action)
    {
        var actionType = action.ActionType;
        var map = RuntimeOutcomeMessageMapBuilder.Create(actionType, string.Empty, string.Empty, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var persistedMap = NormalizeOutcomeMessageMap(actionType, action.OutcomeMessageMap);
        foreach (var (token, script) in persistedMap)
        {
            map[token] = script;
        }

        return map;
    }

    private static Dictionary<string, string> NormalizeOutcomeMessageMap(
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
