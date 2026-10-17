using Storyboard.GameEngine.Configuration.ActionManifest;
using System.IO;

namespace StoryboardDesigner.App.Services;

/// <summary>Shared Designer view of the required indexed action manifest.</summary>
internal static class ActionManifestCatalog
{
    private static readonly Lazy<ActionManifest> Manifest = new(
        () => Load(Path.Combine(AppContext.BaseDirectory, "Config")),
        LazyThreadSafetyMode.ExecutionAndPublication);

    internal static ActionManifest Load(string configDirectory)
    {
        try
        {
            return ActionManifestLoader.Load(configDirectory);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Unable to load required action authoring metadata from '{Path.Combine(configDirectory, "action-manifest.index.json")}'. " +
                "Restore the Storyboard.GameEngine.Configuration package and verify its Config content is deployed.",
                exception);
        }
    }

    internal static IReadOnlyList<ActionManifestDefinition> Actions => Manifest.Value.Actions.Values.ToList();

    internal static ActionManifestDefinition GetAction(string actionType)
    {
        if (!Manifest.Value.Actions.TryGetValue(actionType, out var definition))
        {
            throw new InvalidOperationException($"Action manifest has no definition for action type '{actionType}'.");
        }

        return definition;
    }

    internal static IReadOnlyList<ActionOutputVariableSuggestion> GetOutputVariableSuggestions(
        IEnumerable<string> actionTypes)
    {
        var actionTypeNames = (actionTypes ?? Array.Empty<string>())
            .Where(static actionType => !string.IsNullOrWhiteSpace(actionType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (actionTypeNames.Count == 0)
        {
            return Array.Empty<ActionOutputVariableSuggestion>();
        }

        return Actions
            .Where(definition => actionTypeNames.Contains(definition.ActionType)
                && Enum.TryParse<CommandActionType>(definition.ActionType, ignoreCase: true, out _))
            .SelectMany(static definition => definition.OutputVariables.SelectMany(output =>
            {
                var keys = new List<string> { output.ActionOutputVariableKey };
                if (output.SupportsCount)
                {
                    keys.Add($"{output.ActionOutputVariableKey}Count");
                }

                if (output.SupportsIndexedValues)
                {
                    keys.Add($"{output.ActionOutputVariableKey}[n]");
                }

                return keys.Select(key => new ActionOutputVariableSuggestion(
                    definition.ActionType,
                    key,
                    output.Description ?? string.Empty,
                    output.ValueType ?? string.Empty,
                    output.Notes ?? string.Empty));
            }))
            .GroupBy(static suggestion => $"{suggestion.ActionType}|{suggestion.Key}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static suggestion => suggestion.ActionType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static suggestion => suggestion.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static IReadOnlyList<ActionPayloadArgumentSuggestion> GetPayloadArgumentSuggestions() =>
        Actions
            .SelectMany(static definition => definition.PayloadArguments
                .Where(static argument => !string.IsNullOrWhiteSpace(argument.ActionPayloadArgKey))
                .Select(argument => new ActionPayloadArgumentSuggestion(definition.ActionType, argument.ActionPayloadArgKey)))
            .DistinctBy(static suggestion => $"{suggestion.ActionType}|{suggestion.Key}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(static suggestion => suggestion.ActionType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static suggestion => suggestion.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static IReadOnlyList<string> GetOutputVariableTokens(object actionType) =>
        GetAction(actionType.ToString() ?? string.Empty).OutputVariables.SelectMany(static output =>
        {
            var key = $"currentAction.{output.ActionOutputVariableKey}";
            var tokens = new List<string> { key };
            if (output.SupportsCount)
            {
                tokens.Add($"{key}Count");
            }

            if (output.SupportsIndexedValues)
            {
                tokens.Add($"{key}[n]");
            }

            return tokens;
        }).OrderBy(static token => token, StringComparer.OrdinalIgnoreCase).ToList();

    internal static IReadOnlyList<ActionManifestResultCode> GetSupportedResultCodes(object actionType) =>
        GetAction(actionType.ToString() ?? string.Empty).ResultCodes;

    internal static bool TryGetDescriptor(object actionType, string token, out ActionManifestResultCode descriptor)
    {
        var normalizedToken = token?.Trim() ?? string.Empty;
        descriptor = GetSupportedResultCodes(actionType).FirstOrDefault(result =>
            string.Equals(result.Token, normalizedToken, StringComparison.OrdinalIgnoreCase))!;
        return descriptor is not null;
    }

    internal static IReadOnlyDictionary<string, string> CreateDefaultOutcomeMessageMap(object actionType)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in GetSupportedResultCodes(actionType))
        {
            result[code.Token] = string.Empty;
        }

        return result;
    }
}

internal sealed record ActionOutputVariableSuggestion(
    string ActionType,
    string Key,
    string Description,
    string ValueType,
    string Notes);

internal sealed record ActionPayloadArgumentSuggestion(string ActionType, string Key);
