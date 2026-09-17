using System.IO;
using System.Text.Json;

namespace StoryboardDesigner.App.Services;

public sealed class PhaseTextPresentationCueCatalogService : IPhaseTextPresentationCueCatalogService
{
    private const string CatalogRelativePath = "Storyboard.GameEngine\\Config\\presentation-effects.catalog.json";

    public IReadOnlyList<PhaseTextPresentationCueOption> GetTextCueOptions()
    {
        try
        {
            var catalogPath = ResolveCatalogPath();
            if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
            {
                return Array.Empty<PhaseTextPresentationCueOption>();
            }

            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            if (!document.RootElement.TryGetProperty("effects", out var effects)
                || effects.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<PhaseTextPresentationCueOption>();
            }

            var options = new List<PhaseTextPresentationCueOption>();
            foreach (var effect in effects.EnumerateArray())
            {
                if (!TryGetString(effect, "category", out var category)
                    || !string.Equals(category, "Text", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryGetString(effect, "effectKey", out var effectKey)
                    || string.IsNullOrWhiteSpace(effectKey))
                {
                    continue;
                }

                var displayName = TryGetString(effect, "displayName", out var display)
                    ? display.Trim()
                    : effectKey.Trim();

                string where = string.Empty;
                string? how = null;
                int? durationMs = null;

                if (effect.TryGetProperty("textPresentation", out var textPresentation)
                    && textPresentation.ValueKind == JsonValueKind.Object)
                {
                    _ = TryGetString(textPresentation, "where", out where);
                    _ = TryGetString(textPresentation, "how", out how);
                    durationMs = ResolveTextPresentationDurationMs(textPresentation);
                }

                options.Add(new PhaseTextPresentationCueOption(
                    effectKey.Trim(),
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

    private static string ResolveCatalogPath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, CatalogRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return string.Empty;
    }
}
