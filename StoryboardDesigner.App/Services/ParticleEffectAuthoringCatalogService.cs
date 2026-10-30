using System.IO;
using System.Text.Json;

namespace StoryboardDesigner.App.Services;

/// <summary>One particle recipe exposed for authoring, including its catalog category and declared renderer route.</summary>
/// <param name="EffectKey">Stable recipe key stored by Designer-authored attachments and actions.</param>
/// <param name="DisplayName">Human-readable recipe name from the installed catalog.</param>
/// <param name="Family">Recipe tuning family used to find controls and image slots.</param>
/// <param name="RenderingMode">Catalog route used to decide whether the recipe attaches to objects or environmental scopes.</param>
public sealed record ParticleEffectRecipeOption(string EffectKey, string DisplayName, string Family, string RenderingMode)
{
    /// <summary>Gets the installed catalog category containing this particle recipe.</summary>
    public string Category { get; init; } = string.Empty;
}

public enum ParticleEffectAuthoringContext
{
    ObjectAttachment,
    Environmental
}

public sealed record ParticleEffectControlMetadata(
    string Name,
    string Kind,
    string Unit,
    string Label,
    string Description,
    double? Minimum,
    double? Maximum,
    string ZeroBehavior,
    string? PrimaryRoleLabel = null,
    string? SecondaryRoleLabel = null);

/// <summary>Reads the installed runtime catalog and Designer tuning sidecar as authoring metadata.</summary>
public sealed class ParticleEffectAuthoringCatalogService
{
    private static readonly HashSet<string> ParticleCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "PointParticleEffect",
        "AtmosphericParticleEffect",
        "AtmosphericOverlayEffect",
        "StainEffect"
    };

    private readonly Lazy<CatalogData> _catalog;

    public ParticleEffectAuthoringCatalogService()
    {
        _catalog = new Lazy<CatalogData>(Load);
    }

    public IReadOnlyList<ParticleEffectRecipeOption> GetRecipes() => _catalog.Value.Recipes;

    public IReadOnlyList<ParticleEffectRecipeOption> GetRecipes(ParticleEffectAuthoringContext context) =>
        _catalog.Value.Recipes.Where(recipe => SupportsContext(recipe, context)).ToList();

    internal bool HasBurstEligibleStreams(string? effectKey) =>
        !string.IsNullOrWhiteSpace(effectKey)
        && _catalog.Value.BurstEligibleEffectKeys.Contains(effectKey.Trim());

    public bool SupportsContext(string? effectKey, ParticleEffectAuthoringContext context) =>
        !string.IsNullOrWhiteSpace(effectKey)
        && _catalog.Value.RecipesByKey.TryGetValue(effectKey.Trim(), out var recipe)
        && SupportsContext(recipe, context);

    public IReadOnlyList<ParticleEffectControlMetadata> GetControls(string? effectKey)
    {
        if (string.IsNullOrWhiteSpace(effectKey)
            || !_catalog.Value.RecipesByKey.TryGetValue(effectKey.Trim(), out var recipe)
            || !_catalog.Value.ControlsByFamily.TryGetValue(recipe.Family, out var controls))
        {
            return Array.Empty<ParticleEffectControlMetadata>();
        }

        return controls;
    }

    public IReadOnlyList<string> GetImageSlots(string? effectKey)
    {
        if (string.IsNullOrWhiteSpace(effectKey)
            || !_catalog.Value.RecipesByKey.TryGetValue(effectKey.Trim(), out var recipe))
        {
            return Array.Empty<string>();
        }

        return _catalog.Value.ImageSlotsByFamily.TryGetValue(recipe.Family, out var slots)
            ? slots
            : Array.Empty<string>();
    }

    private static CatalogData Load()
    {
        var configDirectory = Path.Combine(AppContext.BaseDirectory, "Config");
        using var runtime = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "presentation-effects.catalog.json")));
        using var authoring = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "particle-effect-authoring.contract.json")));

        var controlsByFamily = new Dictionary<string, IReadOnlyList<ParticleEffectControlMetadata>>(StringComparer.OrdinalIgnoreCase);
        var imageSlotsByFamily = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in authoring.RootElement.GetProperty("particleEffectFamilyTuningContracts").EnumerateArray())
        {
            var familyName = GetString(family, "family");
            if (string.IsNullOrWhiteSpace(familyName))
            {
                continue;
            }

            controlsByFamily[familyName] = family.GetProperty("controls").EnumerateArray()
                .Select(control => new ParticleEffectControlMetadata(
                    GetString(control, "name"),
                    GetString(control, "kind"),
                    GetString(control, "unit"),
                    GetString(control, "label"),
                    GetString(control, "description"),
                    GetDouble(control, "minimum"),
                    GetDouble(control, "maximum"),
                    GetString(control, "zeroBehavior"),
                    GetNestedString(control, "roleLabels", "primary"),
                    GetNestedString(control, "roleLabels", "secondary")))
                .Where(static control => !string.IsNullOrWhiteSpace(control.Name))
                .ToList();

            imageSlotsByFamily[familyName] = family.TryGetProperty("imageSlots", out var imageSlots)
                ? imageSlots.EnumerateArray()
                    .Select(slot => GetString(slot, "name"))
                    .Where(static name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : Array.Empty<string>();
        }

        var recipes = runtime.RootElement.GetProperty("effects").EnumerateArray()
            .Where(effect => ParticleCategories.Contains(GetString(effect, "category"))
                && effect.TryGetProperty("particleEffect", out _))
            .Select(effect =>
            {
                var recipe = effect.GetProperty("particleEffect").GetProperty("recipe");
                return new ParticleEffectRecipeOption(
                    GetString(effect, "effectKey"),
                    GetString(effect, "displayName"),
                    GetString(recipe, "family"),
                    GetString(recipe, "renderingMode"))
                {
                    Category = GetString(effect, "category")
                };
            })
            .Where(static recipe => !string.IsNullOrWhiteSpace(recipe.EffectKey))
            .OrderBy(static recipe => recipe.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var burstEligibleEffectKeys = runtime.RootElement.GetProperty("effects").EnumerateArray()
            .Where(effect => ParticleCategories.Contains(GetString(effect, "category"))
                && effect.TryGetProperty("particleEffect", out _))
            .Where(effect =>
            {
                var recipe = effect.GetProperty("particleEffect").GetProperty("recipe");
                return recipe.TryGetProperty("streams", out var streams)
                    && streams.ValueKind == JsonValueKind.Array
                    && streams.EnumerateArray().Any(stream => stream.ValueKind == JsonValueKind.Object
                        && stream.TryGetProperty("surgeOnly", out var surgeOnly)
                        && surgeOnly.ValueKind == JsonValueKind.True);
            })
            .Select(effect => GetString(effect, "effectKey"))
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CatalogData(
            recipes,
            recipes.ToDictionary(static recipe => recipe.EffectKey, StringComparer.OrdinalIgnoreCase),
            burstEligibleEffectKeys,
            controlsByFamily,
            imageSlotsByFamily);
    }

    // renderingMode is the catalog's declared renderer route. Point and stain routes
    // require an object anchor; room and overlay routes are authored at geographic scopes.
    private static bool SupportsContext(ParticleEffectRecipeOption recipe, ParticleEffectAuthoringContext context) =>
        context switch
        {
            ParticleEffectAuthoringContext.ObjectAttachment =>
                string.Equals(recipe.RenderingMode, "point", StringComparison.OrdinalIgnoreCase)
                || string.Equals(recipe.RenderingMode, "stain", StringComparison.OrdinalIgnoreCase),
            ParticleEffectAuthoringContext.Environmental =>
                string.Equals(recipe.RenderingMode, "room", StringComparison.OrdinalIgnoreCase)
                || string.Equals(recipe.RenderingMode, "overlay", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static double? GetDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var number)
            ? number
            : null;

    private static string? GetNestedString(JsonElement element, string objectName, string propertyName) =>
        element.TryGetProperty(objectName, out var parent) && parent.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    private sealed record CatalogData(
        IReadOnlyList<ParticleEffectRecipeOption> Recipes,
        IReadOnlyDictionary<string, ParticleEffectRecipeOption> RecipesByKey,
        IReadOnlySet<string> BurstEligibleEffectKeys,
        IReadOnlyDictionary<string, IReadOnlyList<ParticleEffectControlMetadata>> ControlsByFamily,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ImageSlotsByFamily);
}
