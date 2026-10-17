using System.IO;
using System.Text.Json;
using Storyboard.GameEngine.Configuration.Catalogs;

namespace StoryboardDesigner.App.Services;

public sealed class PhaseTextPresentationCueCatalogService : IPhaseTextPresentationCueCatalogService
{
    public IReadOnlyList<PhaseTextPresentationCueOption> GetTextCueOptions()
    {
        try
        {
            var catalog = PresentationEffectsCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "Config"));
            var options = new List<PhaseTextPresentationCueOption>();
            foreach (var effect in catalog.Effects)
            {
                if (!string.Equals(effect.Category, "Text", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var effectKey = effect.EffectKey?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(effectKey))
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(effect.DisplayName)
                    ? effectKey
                    : effect.DisplayName.Trim();

                string where = string.Empty;
                string? how = null;
                int? durationMs = null;

                if (effect.TextPresentation is JsonElement textPresentation
                    && textPresentation.ValueKind == JsonValueKind.Object)
                {
                    _ = TryGetString(textPresentation, "where", out where);
                    _ = TryGetString(textPresentation, "how", out how);
                    durationMs = ResolveTextPresentationDurationMs(textPresentation);
                }

                options.Add(new PhaseTextPresentationCueOption(
                    effectKey,
                    displayName,
                    where?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(how) ? null : how.Trim(),
                    durationMs));
            }

            return options
                .OrderBy(static option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static option => option.EffectKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<PhaseTextPresentationCueOption>();
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var node)
            || node.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = node.GetString() ?? string.Empty;
        return true;
    }

    private static int? ResolveTextPresentationDurationMs(JsonElement textPresentation)
    {
        if (textPresentation.TryGetProperty("durationMs", out var directDuration)
            && TryGetInt32(directDuration, out var parsedDirect))
        {
            return parsedDirect;
        }

        if (textPresentation.TryGetProperty("duration", out var durationObject)
            && durationObject.ValueKind == JsonValueKind.Object
            && durationObject.TryGetProperty("durationMs", out var nestedDuration)
            && TryGetInt32(nestedDuration, out var parsedNested))
        {
            return parsedNested;
        }

        if (textPresentation.TryGetProperty("howLong", out var howLongObject)
            && howLongObject.ValueKind == JsonValueKind.Object
            && howLongObject.TryGetProperty("durationMs", out var legacyDuration)
            && TryGetInt32(legacyDuration, out var parsedLegacy))
        {
            return parsedLegacy;
        }

        return null;
    }

    private static bool TryGetInt32(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

}
