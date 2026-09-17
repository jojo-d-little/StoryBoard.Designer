using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class EffectiveObjectFieldResolver
{
    public const string NameInGameField = "NameInGame";
    public const string NameSynonymsField = "NameSynonyms";
    public const string DescriptionField = "Description";

    public static bool HasOverride(GameObject instance, string fieldKey)
    {
        return instance.InstanceOverrides.ContainsKey(fieldKey);
    }

    public static void SetOverride(GameObject instance, string fieldKey, string? value)
    {
        instance.InstanceOverrides[fieldKey] = value;
    }

    public static void ClearOverride(GameObject instance, string fieldKey)
    {
        instance.InstanceOverrides.Remove(fieldKey);
    }

    public static string GetEffectiveName(GameObject instance, Func<Guid, GameObject?> resolveDefinition)
    {
        return instance.Name ?? string.Empty;
    }

    public static string GetEffectiveNameInGame(GameObject instance, Func<Guid, GameObject?> resolveDefinition)
    {
        if (!string.IsNullOrWhiteSpace(instance.NameInGame))
        {
            return instance.NameInGame;
        }

        if (TryGetOverride(instance, NameInGameField, out var overrideValue))
        {
            return overrideValue ?? string.Empty;
        }

        return instance.NameInGame ?? string.Empty;
    }

    public static IReadOnlyList<string> GetEffectiveNameSynonyms(GameObject instance, Func<Guid, GameObject?> resolveDefinition)
    {
        if (TryGetOverride(instance, NameSynonymsField, out var overrideValue))
        {
            return ParseNameSynonyms(overrideValue);
        }

        var definition = ResolveDefinition(instance, resolveDefinition);
        if (definition is not null)
        {
            return NormalizeNameSynonyms(definition.NameSynonyms);
        }

        return NormalizeNameSynonyms(instance.NameSynonyms);
    }

    public static string GetEffectiveDescription(GameObject instance, Func<Guid, GameObject?> resolveDefinition)
    {
        if (TryGetOverride(instance, DescriptionField, out var overrideValue))
        {
            return overrideValue ?? string.Empty;
        }

        var definition = ResolveDefinition(instance, resolveDefinition);
        if (definition is not null)
        {
            return definition.Description ?? string.Empty;
        }

        return instance.Description ?? string.Empty;
    }

    public static DefinitionOwnedOverridePlan BuildDefinitionOwnedOverridePlan(
        GameObject instance,
        GameObject? definition,
        string updatedNameInGame,
        IReadOnlyList<string> updatedNameSynonyms,
        string updatedDescription)
    {
        if (definition is null)
        {
            return DefinitionOwnedOverridePlan.Unlinked;
        }

        var desiredNameSynonymsOverride = AreEquivalentNameSynonyms(updatedNameSynonyms, definition.NameSynonyms)
            ? null
            : SerializeNameSynonymsOverride(updatedNameSynonyms);
        var desiredDescriptionOverride = string.Equals(updatedDescription, definition.Description, StringComparison.Ordinal)
            ? null
            : updatedDescription;

        return new DefinitionOwnedOverridePlan(
            IsLinkedInstance: true,
            NameInGameOverride: null,
            NameSynonymsOverride: desiredNameSynonymsOverride,
            DescriptionOverride: desiredDescriptionOverride,
            HasNameInGameOverrideChange: false,
            HasNameSynonymsOverrideChange: HasOverrideChange(instance, NameSynonymsField, desiredNameSynonymsOverride),
            HasDescriptionOverrideChange: HasOverrideChange(instance, DescriptionField, desiredDescriptionOverride));
    }

    public static void ApplyDefinitionOwnedOverrides(GameObject instance, DefinitionOwnedOverridePlan plan)
    {
        if (!plan.IsLinkedInstance)
        {
            return;
        }

        ApplyOverrideValue(instance, NameInGameField, plan.NameInGameOverride);
        ApplyOverrideValue(instance, NameSynonymsField, plan.NameSynonymsOverride);
        ApplyOverrideValue(instance, DescriptionField, plan.DescriptionOverride);
    }

    private static bool HasOverrideChange(GameObject instance, string fieldKey, string? desiredValue)
    {
        var hasCurrent = instance.InstanceOverrides.TryGetValue(fieldKey, out var currentValue);
        if (desiredValue is null)
        {
            return hasCurrent;
        }

        return !hasCurrent || !string.Equals(currentValue, desiredValue, StringComparison.Ordinal);
    }

    private static void ApplyOverrideValue(GameObject instance, string fieldKey, string? desiredValue)
    {
        if (desiredValue is null)
        {
            ClearOverride(instance, fieldKey);
            return;
        }

        SetOverride(instance, fieldKey, desiredValue);
    }

    private static bool TryGetOverride(GameObject instance, string fieldKey, out string? value)
    {
        return instance.InstanceOverrides.TryGetValue(fieldKey, out value);
    }

    public static string SerializeNameSynonymsOverride(IEnumerable<string>? synonyms)
    {
        return string.Join(", ", NormalizeNameSynonyms(synonyms));
    }

    private static List<string> ParseNameSynonyms(string? raw)
    {
        return NormalizeNameSynonyms((raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> NormalizeNameSynonyms(IEnumerable<string>? synonyms)
    {
        return (synonyms ?? Array.Empty<string>())
            .Select(static synonym => synonym?.Trim() ?? string.Empty)
            .Where(static synonym => !string.IsNullOrWhiteSpace(synonym))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool AreEquivalentNameSynonyms(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        var normalizedLeft = NormalizeNameSynonyms(left);
        var normalizedRight = NormalizeNameSynonyms(right);
        return normalizedLeft.SequenceEqual(normalizedRight, StringComparer.OrdinalIgnoreCase);
    }

    private static GameObject? ResolveDefinition(GameObject instance, Func<Guid, GameObject?> resolveDefinition)
    {
        if (!instance.LinkedBaseObjectId.HasValue)
        {
            return null;
        }

        return resolveDefinition(instance.LinkedBaseObjectId.Value);
    }
}

public readonly record struct DefinitionOwnedOverridePlan(
    bool IsLinkedInstance,
    string? NameInGameOverride,
    string? NameSynonymsOverride,
    string? DescriptionOverride,
    bool HasNameInGameOverrideChange,
    bool HasNameSynonymsOverrideChange,
    bool HasDescriptionOverrideChange)
{
    public static DefinitionOwnedOverridePlan Unlinked { get; } =
        new(
            IsLinkedInstance: false,
            NameInGameOverride: null,
            NameSynonymsOverride: null,
            DescriptionOverride: null,
            HasNameInGameOverrideChange: false,
            HasNameSynonymsOverrideChange: false,
            HasDescriptionOverrideChange: false);
}
