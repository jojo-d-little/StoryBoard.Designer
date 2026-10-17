using System.IO;
using Storyboard.GameEngine.Configuration.Catalogs;
using StoryboardDesigner.App.Models;

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

    public IReadOnlyList<MovementEffectChoiceItem> GetConfiguredMovementEffects()
    {
        return _catalog.Value.Effects
            .Where(static effect => string.Equals(effect.Category, "Movement", StringComparison.OrdinalIgnoreCase))
            .Select(static effect => new MovementEffectChoiceItem(
                effect.EffectKey?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(effect.DisplayName) ? effect.EffectKey?.Trim() ?? string.Empty : effect.DisplayName.Trim()))
            .Where(static effect => !string.IsNullOrWhiteSpace(effect.EffectKey))
            .GroupBy(static effect => effect.EffectKey, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static effect => effect.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string GetDefaultMovementEffectKey()
    {
        return GetConfiguredMovementEffects()
            .FirstOrDefault(static effect => string.Equals(
                effect.EffectKey,
                MovementPresentationEffectKeys.DefaultMedium,
                StringComparison.OrdinalIgnoreCase))?.EffectKey
            ?? string.Empty;
    }

    public bool IsConfiguredMovementEffectKey(string? effectKey)
    {
        if (string.IsNullOrWhiteSpace(effectKey))
        {
            return false;
        }

        return _catalog.Value.Effects.Any(effect =>
            string.Equals(effect.Category, "Movement", StringComparison.OrdinalIgnoreCase)
            && string.Equals(effect.EffectKey?.Trim(), effectKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public string GetMovementEffectDisplayName(string? effectKey)
    {
        return GetConfiguredMovementEffects()
            .FirstOrDefault(effect => string.Equals(effect.EffectKey, effectKey?.Trim(), StringComparison.OrdinalIgnoreCase))?.DisplayName
            ?? effectKey?.Trim()
            ?? string.Empty;
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
