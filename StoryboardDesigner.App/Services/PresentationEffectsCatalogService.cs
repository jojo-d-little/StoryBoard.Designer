using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.RuntimeContext;
using System.IO;
using System.Text.Json;

namespace StoryboardDesigner.App.Services;

public sealed class PresentationEffectsCatalogService
{
    private readonly Lazy<GameCommandPresentationEffectsCatalog> _catalog;

    public PresentationEffectsCatalogService()
    {
        _catalog = new Lazy<GameCommandPresentationEffectsCatalog>(LoadCatalog);
    }

    public GameCommandPresentationEffectsCatalog GetCatalog()
    {
        return _catalog.Value;
    }

    public IReadOnlyList<RuntimeMovementVisualTransitionHint> GetConfiguredMovementHints()
    {
        var ordered = _catalog.Value.Effects
            .Where(static effect => effect.Category == HostCommandPresentationCueCategory.Movement)
            .Where(static effect => effect.MovementHint.HasValue)
            .Select(static effect => effect.MovementHint!.Value)
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
            .Where(static effect => effect.Category == HostCommandPresentationCueCategory.RoomTransition)
            .Select(static effect => effect.EffectKey?.Trim() ?? string.Empty)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetObjectCueEligibleEffectKeys()
    {
        var catalogPath = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Config", "presentation-effects.catalog.json"),
            Path.Combine(AppContext.BaseDirectory, "Storyboard.GameEngine", "Config", "presentation-effects.catalog.json")
        }.FirstOrDefault(File.Exists);
        if (catalogPath is null)
        {
            return Array.Empty<string>();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var eligibleCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (document.RootElement.TryGetProperty("categories", out var categories)
            && categories.ValueKind == JsonValueKind.Array)
        {
            foreach (var category in categories.EnumerateArray())
            {
                if (category.TryGetProperty("objectCueEligible", out var eligible)
                    && eligible.ValueKind == JsonValueKind.True
                    && category.TryGetProperty("category", out var name)
                    && name.ValueKind == JsonValueKind.String)
                {
                    eligibleCategories.Add(name.GetString() ?? string.Empty);
                }
            }
        }

        if (!document.RootElement.TryGetProperty("effects", out var effects)
            || effects.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return effects.EnumerateArray()
            .Where(effect => effect.TryGetProperty("category", out var category)
                && category.ValueKind == JsonValueKind.String
                && eligibleCategories.Contains(category.GetString() ?? string.Empty))
            .Select(effect => effect.TryGetProperty("effectKey", out var key) && key.ValueKind == JsonValueKind.String
                ? key.GetString()?.Trim() ?? string.Empty
                : string.Empty)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static GameCommandPresentationEffectsCatalog LoadCatalog()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "presentation-effects.catalog.json");
        return GameCommandPresentationEffectsCatalogLoader.LoadFromPathOrDefault(configPath);
    }
}
