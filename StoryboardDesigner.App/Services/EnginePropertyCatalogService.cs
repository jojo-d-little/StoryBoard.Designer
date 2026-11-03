using Storyboard.GameEngine.Configuration.Catalogs;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Models.Enums;
using System.IO;

namespace StoryboardDesigner.App.Services;

/// <summary>Provides Engine-owned well-known property descriptors to Designer authoring.</summary>
internal static class EnginePropertyCatalogService
{
    private static readonly Lazy<EnginePropertyCatalog> Catalog = new(
        () => EnginePropertyCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "Config")),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<EnginePropertyDescriptor> Properties => Catalog.Value.Properties;

    public static IReadOnlyList<EnginePropertyDescriptor> ForScope(PropertyResolutionScope scope) =>
        Properties.Where(property => property.Scopes.Contains(ToEngineScope(scope), StringComparer.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyList<EnginePropertyDescriptor> ForOwner(IScopedAwareNode owner) =>
        ForScope(owner switch
        {
            GameObject => PropertyResolutionScope.Object,
            Room => PropertyResolutionScope.Room,
            Area => PropertyResolutionScope.Area,
            Country => PropertyResolutionScope.Country,
            Planet => PropertyResolutionScope.Planet,
            _ => PropertyResolutionScope.Global
        });

    public static bool CanWrite(EnginePropertyDescriptor descriptor) =>
        string.Equals(descriptor.WritePolicy, "SetGameProperty", StringComparison.OrdinalIgnoreCase);

    public static string BuildDocumentation(EnginePropertyDescriptor descriptor)
    {
        var neutral = descriptor.NeutralValue is null
            ? string.Empty
            : $" Neutral value: {descriptor.NeutralValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}.";
        return $"{descriptor.Meaning} {descriptor.Usage} {descriptor.MutationSemantics}{neutral}";
    }

    public static GamePropertyValueRestriction ToValueRestriction(EnginePropertyDescriptor descriptor) =>
        descriptor.ValueType.ToLowerInvariant() switch
        {
            "number" => GamePropertyValueRestriction.Numeric,
            "boolean" => GamePropertyValueRestriction.TrueFalse,
            _ => GamePropertyValueRestriction.Unrestricted
        };

    public static string ToEngineScope(PropertyResolutionScope scope) => scope switch
    {
        PropertyResolutionScope.Object => "GameObject",
        PropertyResolutionScope.Room => "Room",
        PropertyResolutionScope.Area => "Area",
        PropertyResolutionScope.Country => "Country",
        PropertyResolutionScope.Planet => "Planet",
        PropertyResolutionScope.PhaseBook => "Book",
        PropertyResolutionScope.PhaseChapter => "Chapter",
        PropertyResolutionScope.PhasePage => "Page",
        _ => string.Empty
    };
}
