using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.RuntimeContext;
using System.IO;

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

    private static GameCommandPresentationEffectsCatalog LoadCatalog()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "presentation-effects.catalog.json");
        return GameCommandPresentationEffectsCatalogLoader.LoadFromPathOrDefault(configPath);
    }
}
