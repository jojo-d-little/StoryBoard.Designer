using System.IO;
using Storyboard.GameEngine.Configuration.Catalogs;
using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Services;

public sealed class PresentationEffectsCatalogService
{
    private const string DefaultPrimarySelectionOutlineEffectKey = "appearance.selection.outline.cyan";
    private const string DefaultSecondarySelectionOutlineEffectKey = "appearance.selection.outline.amber";
    private const string SelectionOutlineEffectKeyPrefix = "appearance.selection.outline.";

    private readonly Lazy<PresentationEffectsCatalog> _catalog;

    public PresentationEffectsCatalogService()
    {
        _catalog = new Lazy<PresentationEffectsCatalog>(LoadCatalog);
    }

    public PresentationEffectsCatalog GetCatalog()
    {
        return _catalog.Value;
    }

    public IReadOnlyList<RuntimeMovementVisualTransitionHint> GetConfiguredMovementHints()
    {
        var ordered = _catalog.Value.Effects
            .Where(static effect => string.Equals(effect.Category, "Movement", StringComparison.OrdinalIgnoreCase))
            .Select(static effect => Enum.TryParse<RuntimeMovementVisualTransitionHint>(effect.MovementHint, true, out var hint)
                ? (RuntimeMovementVisualTransitionHint?)hint
                : null)
            .Where(static hint => hint.HasValue)
            .Select(static hint => hint!.Value)
            .Distinct()
            .ToList();

        foreach (var fallback in Enum.GetValues<RuntimeMovementVisualTransitionHint>())
        {
            if (!ordered.Contains(fallback))
            {
                ordered.Add(fallback);
            }
        }

        return ordered;
    }

    public IReadOnlyList<string> GetConfiguredRoomTransitionEffectKeys()
    {
        return _catalog.Value.Effects
            .Where(static effect => string.Equals(effect.Category, "RoomTransition", StringComparison.OrdinalIgnoreCase))
            .Select(static effect => effect.EffectKey?.Trim() ?? string.Empty)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetObjectCueEligibleEffectKeys()
    {
        var eligibleCategories = _catalog.Value.Categories
            .Where(static category => category.ObjectCueEligible)
            .Select(static category => category.Category)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _catalog.Value.Effects
            .Where(effect => eligibleCategories.Contains(effect.Category))
            .Select(static effect => effect.EffectKey?.Trim() ?? string.Empty)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string GetDefaultSelectionOutlineEffectKey()
    {
        var configuredKeys = GetConfiguredSelectionOutlineEffectKeys();
        return configuredKeys.FirstOrDefault(key => string.Equals(
                   key,
                   DefaultPrimarySelectionOutlineEffectKey,
                   StringComparison.OrdinalIgnoreCase))
               ?? configuredKeys.FirstOrDefault()
               ?? DefaultPrimarySelectionOutlineEffectKey;
    }

    public string GetDefaultSecondarySelectionOutlineEffectKey()
    {
        var configuredKeys = GetConfiguredSelectionOutlineEffectKeys();
        return configuredKeys.FirstOrDefault(key => string.Equals(
                   key,
                   DefaultSecondarySelectionOutlineEffectKey,
                   StringComparison.OrdinalIgnoreCase))
               ?? configuredKeys.FirstOrDefault(key => !string.Equals(
                   key,
                   GetDefaultSelectionOutlineEffectKey(),
                   StringComparison.OrdinalIgnoreCase))
               ?? DefaultSecondarySelectionOutlineEffectKey;
    }

    private IReadOnlyList<string> GetConfiguredSelectionOutlineEffectKeys()
    {
        return _catalog.Value.Effects
            .Where(static effect => string.Equals(effect.Category, "Appearance", StringComparison.OrdinalIgnoreCase))
            .Select(static effect => effect.EffectKey?.Trim() ?? string.Empty)
            .Where(static key => key.StartsWith(SelectionOutlineEffectKeyPrefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PresentationEffectsCatalog LoadCatalog()
    {
        return PresentationEffectsCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "Config"));
    }
}
