using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Models.Enums;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Enumerates and resolves only properties that are defined on project owners.
/// Both the variable picker and effect validation use this source of truth.
/// </summary>
internal static class GamePropertySourceResolver
{
    public sealed record CatalogNameCollision(
        Guid OwnerId,
        string OwnerName,
        string ScopePath,
        PropertyResolutionScope Scope,
        string PropertyName);

    public sealed record Definition(
        ScopeNodeBase Owner,
        Guid OwnerId,
        string OwnerName,
        string ScopePath,
        PropertyResolutionScope Scope,
        GamePropertyDefinition Property,
        IReadOnlyList<string> EligibleAnchors,
        bool IsEngineProperty = false,
        bool CanWrite = true,
        string? Documentation = null);

    public static IReadOnlyList<Definition> Enumerate(ProjectModel project)
    {
        return EnumerateOwners(project)
            .SelectMany(owner => owner.Properties.Select(property =>
            {
                var descriptor = EnginePropertyCatalogService.Properties.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, property.Name, StringComparison.OrdinalIgnoreCase)
                    && candidate.Scopes.Contains(EnginePropertyCatalogService.ToEngineScope(owner.Scope), StringComparer.OrdinalIgnoreCase));
                return new Definition(
                    owner.Owner,
                    owner.OwnerId,
                    owner.OwnerName,
                    owner.ScopePath,
                    owner.Scope,
                    property,
                    owner.EligibleAnchors,
                    descriptor is not null,
                    descriptor is null || EnginePropertyCatalogService.CanWrite(descriptor),
                    descriptor is null ? null : EnginePropertyCatalogService.BuildDocumentation(descriptor));
            }))
            .ToList();
    }

    public static IReadOnlyList<CatalogNameCollision> EnumerateCatalogNameCollisions(ProjectModel project)
    {
        return EnumerateOwners(project)
            .SelectMany(owner => owner.AuthoredProperties
                .Where(property => owner.CatalogKeys.Contains(property.Name.Trim(), StringComparer.OrdinalIgnoreCase))
                .Select(property => new CatalogNameCollision(
                    owner.OwnerId,
                    owner.OwnerName,
                    owner.ScopePath,
                    owner.Scope,
                    property.Name.Trim())))
            .DistinctBy(collision => (collision.OwnerId, collision.PropertyName), CatalogNameCollisionKeyComparer.Instance)
            .ToList();
    }

    private sealed record OwnerDefinition(
        ScopeNodeBase Owner,
        Guid OwnerId,
        string OwnerName,
        string ScopePath,
        PropertyResolutionScope Scope,
        IReadOnlyList<GamePropertyDefinition> Properties,
        IReadOnlyList<GamePropertyDefinition> AuthoredProperties,
        IReadOnlyList<string> CatalogKeys,
        IReadOnlyList<string> EligibleAnchors);

    private static IReadOnlyList<OwnerDefinition> EnumerateOwners(ProjectModel project)
    {
        var result = new List<OwnerDefinition>();
        var playerName = project.PlayerCharacterObjectName?.Trim() ?? string.Empty;

        void Add(ScopeNodeBase owner, IEnumerable<GamePropertyDefinition> properties, IReadOnlyList<string>? anchors = null)
        {
            var ownerId = GetOwnerId(owner);
            var ownerName = owner.ScopeName;
            var scopePath = GetScopePath(owner);
            var scope = GetResolutionScope(owner);
            var eligibleAnchors = anchors ?? Array.Empty<string>();
            var authoredProperties = properties.Where(static property => !string.IsNullOrWhiteSpace(property.Name)).ToList();
            var descriptors = EnginePropertyCatalogService.ForScope(scope)
                .GroupBy(static descriptor => descriptor.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToList();
            var catalogKeys = descriptors.Select(static descriptor => descriptor.Key).ToList();
            var ownerProperties = authoredProperties
                .Where(property => !catalogKeys.Contains(property.Name.Trim(), StringComparer.OrdinalIgnoreCase))
                .GroupBy(static property => property.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToList();
            foreach (var descriptor in descriptors)
            {
                ownerProperties.Add(new GamePropertyDefinition
                {
                    Name = descriptor.Key,
                    DefaultValue = descriptor.NeutralValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    ValueRestriction = EnginePropertyCatalogService.ToValueRestriction(descriptor)
                });
            }
            result.Add(new OwnerDefinition(
                owner,
                ownerId,
                ownerName,
                scopePath,
                scope,
                ownerProperties,
                authoredProperties,
                catalogKeys,
                eligibleAnchors));
        }

        void AddObject(GameObject obj)
        {
            var isPlayer = (!string.IsNullOrWhiteSpace(playerName)
                    && string.Equals(obj.Name, playerName, StringComparison.OrdinalIgnoreCase))
                || obj.Variables.Any(property => string.Equals(property.Name, "isPlayer", StringComparison.OrdinalIgnoreCase)
                    && bool.TryParse(property.DefaultValue, out var markedPlayer) && markedPlayer);
            Add(obj, obj.Variables, isPlayer ? ["activePlayer"] : null);
            foreach (var child in obj.ContainedObjects) AddObject(child);
        }

        void AddObjects(IEnumerable<GameObject> objects)
        {
            foreach (var obj in objects) AddObject(obj);
        }

        void AddPhase(PhaseNode phase)
        {
            // Page weather properties are generated GamePropertyDefinition instances.
            // Keep their normal definitions in the same source set used by validation.
            PhaseEnvironmentalProperties.MaterializePageProperties(phase);
            Add(phase, phase.Variables, ["currentPhase"]);
            foreach (var child in phase.Children) AddPhase(child);
        }

        Add(project, project.GlobalVariables);
        AddObjects(project.GlobalScope.GameObjects);
        AddObjects(project.ObjectTemplates);
        AddObjects(project.BaseObjects);
        foreach (var room in project.EnumerateAllRoomTemplateRoots().DistinctBy(static room => room.Id))
        {
            Add(room, room.Variables, ["currentRoom"]);
            AddObjects(room.GameObjects);
        }
        foreach (var phase in project.PhaseBooks) AddPhase(phase);
        foreach (var planet in project.Planets)
        {
            Add(planet, planet.Variables, ["currentPlanet"]);
            AddObjects(planet.BaseObjects);
            AddObjects(planet.ObjectTemplates);
            AddObjects(planet.GameObjects);
            foreach (var country in planet.Countries)
            {
                Add(country, country.Variables, ["currentCountry"]);
                AddObjects(country.BaseObjects);
                AddObjects(country.ObjectTemplates);
                AddObjects(country.GameObjects);
                foreach (var area in country.Areas)
                {
                    Add(area, area.Variables, ["currentArea"]);
                    AddObjects(area.BaseObjects);
                    AddObjects(area.ObjectTemplates);
                    AddObjects(area.GameObjects);
                    foreach (var room in area.Rooms)
                    {
                        Add(room, room.Variables, ["currentRoom"]);
                        AddObjects(room.GameObjects);
                    }
                }
            }
        }

        return result;
    }

    public static IReadOnlyList<Definition> Resolve(
        ProjectModel project,
        ProjectGamePropertySourceReferenceDto source,
        Guid relativeOwnerId)
    {
        var definitions = Enumerate(project);
        var targetOwnerId = source.OwnerId ?? relativeOwnerId;
        return definitions.Where(definition =>
                (string.IsNullOrWhiteSpace(source.AnchorKey)
                    ? definition.OwnerId == targetOwnerId
                    : definition.EligibleAnchors.Contains(source.AnchorKey.Trim(), StringComparer.OrdinalIgnoreCase))
                && string.Equals(definition.Property.Name, source.PropertyName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static bool SupportsAnchor(ProjectModel project, string anchorKey) =>
        EnumerateOwners(project).Any(owner => owner.EligibleAnchors.Contains(anchorKey, StringComparer.OrdinalIgnoreCase));

    private sealed class CatalogNameCollisionKeyComparer : IEqualityComparer<(Guid OwnerId, string PropertyName)>
    {
        public static CatalogNameCollisionKeyComparer Instance { get; } = new();

        public bool Equals((Guid OwnerId, string PropertyName) x, (Guid OwnerId, string PropertyName) y) =>
            x.OwnerId == y.OwnerId && string.Equals(x.PropertyName, y.PropertyName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid OwnerId, string PropertyName) value) =>
            HashCode.Combine(value.OwnerId, StringComparer.OrdinalIgnoreCase.GetHashCode(value.PropertyName));
    }

    private static Guid GetOwnerId(ScopeNodeBase owner) => owner switch
    {
        GameObject gameObject => gameObject.ObjectId,
        Room room => room.Id,
        Planet planet => planet.Id,
        Country country => country.Id,
        Area area => area.Id,
        PhaseNode phase => phase.Id,
        ProjectModel => Guid.Empty,
        _ => Guid.Empty
    };

    private static PropertyResolutionScope GetResolutionScope(ScopeNodeBase owner) => owner switch
    {
        ProjectModel => PropertyResolutionScope.Global,
        GameObject => PropertyResolutionScope.Object,
        Room => PropertyResolutionScope.Room,
        Area => PropertyResolutionScope.Area,
        Country => PropertyResolutionScope.Country,
        Planet => PropertyResolutionScope.Planet,
        PhaseNode { Tier: PhaseTier.Book } => PropertyResolutionScope.PhaseBook,
        PhaseNode { Tier: PhaseTier.Chapter } => PropertyResolutionScope.PhaseChapter,
        PhaseNode => PropertyResolutionScope.PhasePage,
        _ => PropertyResolutionScope.Global
    };

    private static string GetScopePath(ScopeNodeBase owner)
    {
        var parts = new Stack<string>();
        for (IScopedAwareNode? current = owner; current is not null; current = current.ParentScope)
        {
            if (!string.IsNullOrWhiteSpace(current.ScopeName)) parts.Push(current.ScopeName);
        }
        return string.Join('.', parts);
    }
}
