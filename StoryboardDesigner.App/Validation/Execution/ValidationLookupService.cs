using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed class ValidationLookupService : IValidationLookupService
{
    private readonly IReadOnlyDictionary<Guid, ValidationGamePropertyDescriptor> _propertyById;
    private readonly IReadOnlyDictionary<Guid, ValidationSharedPropertyRelationship> _sharedBySourceVariableId;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<ValidationSharedPropertyRelationship>> _sharedByVariableId;

    private ValidationLookupService(
        IReadOnlyDictionary<Guid, ValidationGamePropertyDescriptor> propertyById,
        IReadOnlyDictionary<Guid, ValidationSharedPropertyRelationship> sharedBySourceVariableId,
        IReadOnlyDictionary<Guid, IReadOnlyList<ValidationSharedPropertyRelationship>> sharedByVariableId)
    {
        _propertyById = propertyById;
        _sharedBySourceVariableId = sharedBySourceVariableId;
        _sharedByVariableId = sharedByVariableId;
    }

    public static IValidationLookupService Build(ProjectModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var propertyById = BuildPropertyLookup(project);
        var relationships = BuildSharedRelationshipLookup(project, propertyById);

        var bySource = relationships
            .GroupBy(static relationship => relationship.SourceVariableId)
            .ToDictionary(group => group.Key, group => group.First());

        var byEitherVariable = relationships
            .SelectMany(relationship => new[]
            {
                new KeyValuePair<Guid, ValidationSharedPropertyRelationship>(relationship.SourceVariableId, relationship),
                new KeyValuePair<Guid, ValidationSharedPropertyRelationship>(relationship.TargetVariableId, relationship)
            })
            .GroupBy(pair => pair.Key)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ValidationSharedPropertyRelationship>)group.Select(entry => entry.Value).ToList());

        return new ValidationLookupService(propertyById, bySource, byEitherVariable);
    }

    public bool TryGetSharedPropertyRelationship(Guid sourceVariableId, out ValidationSharedPropertyRelationship relationship)
    {
        return _sharedBySourceVariableId.TryGetValue(sourceVariableId, out relationship!);
    }

    public bool TryGetPropertyByVariableId(Guid variableId, out ValidationGamePropertyDescriptor property)
    {
        return _propertyById.TryGetValue(variableId, out property!);
    }

    public IReadOnlyList<ValidationSharedPropertyRelationship> GetSharedPropertyRelationshipsForVariable(Guid variableId)
    {
        return _sharedByVariableId.TryGetValue(variableId, out var relationships)
            ? relationships
            : Array.Empty<ValidationSharedPropertyRelationship>();
    }

    private static Dictionary<Guid, ValidationGamePropertyDescriptor> BuildPropertyLookup(ProjectModel project)
    {
        var map = new Dictionary<Guid, ValidationGamePropertyDescriptor>();

        void AddRange(IEnumerable<GamePropertyDefinition> variables, string scopePath, string? semanticHint = null)
        {
            foreach (var variable in variables)
            {
                if (variable.Id == Guid.Empty)
                {
                    continue;
                }

                map[variable.Id] = new ValidationGamePropertyDescriptor(
                    variable.Id,
                    variable.Name,
                    scopePath,
                    variable.ValueRestriction,
                    variable.SharedVariableId.HasValue && variable.SharedVariableId.Value != Guid.Empty,
                    semanticHint);
            }
        }

        void AddObjects(IEnumerable<GameObject> roots, string scopePath, string semanticHint)
        {
            foreach (var root in roots)
            {
                AddObject(root);
            }

            void AddObject(GameObject gameObject)
            {
                var effectiveProperties = EffectiveGamePropertyResolver.Resolve(gameObject, id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id));
                AddRange(effectiveProperties.Select(static property => property.Definition), scopePath, semanticHint);
                AddObjects(gameObject.ContainedObjects, scopePath, semanticHint);
            }
        }

        AddRange(project.GlobalVariables, "Global", "ProjectGlobalVariable");
        AddRange(project.GlobalScope.GameProperties, "Global / Game Properties", "GlobalScopeVariable");
        AddObjects(project.BaseObjects, "Global / Base Objects", "BaseObjectVariable");
        AddObjects(project.GlobalScope.GameObjects, "Global / Game Objects", "GameObjectVariable");
        AddObjects(project.EnumerateAllObjectTemplateRoots(), "Global / Templates", "TemplateObjectVariable");
        AddObjects(
            project.EnumerateAllRoomTemplateRoots().SelectMany(static room => room.GameObjects),
            "Global / Room Templates",
            "TemplateObjectVariable");

        foreach (var planet in project.Planets)
        {
            AddRange(planet.Variables, $"Global / {planet.Name}", "PlanetVariable");
            AddObjects(planet.BaseObjects, $"Global / {planet.Name} / Base Objects", "BaseObjectVariable");
            AddObjects(planet.ObjectTemplates, $"Global / {planet.Name} / Templates", "TemplateObjectVariable");
            AddObjects(planet.GameObjects, $"Global / {planet.Name} / Game Objects", "GameObjectVariable");

            foreach (var country in planet.Countries)
            {
                AddRange(country.Variables, $"Global / {planet.Name} / {country.Name}", "CountryVariable");
                AddObjects(country.BaseObjects, $"Global / {planet.Name} / {country.Name} / Base Objects", "BaseObjectVariable");
                AddObjects(country.ObjectTemplates, $"Global / {planet.Name} / {country.Name} / Templates", "TemplateObjectVariable");
                AddObjects(country.GameObjects, $"Global / {planet.Name} / {country.Name} / Game Objects", "GameObjectVariable");

                foreach (var area in country.Areas)
                {
                    var areaPath = $"Global / {planet.Name} / {country.Name} / {area.Name}";
                    AddRange(area.Variables, areaPath, "AreaVariable");
                    AddObjects(area.BaseObjects, areaPath + " / Base Objects", "BaseObjectVariable");
                    AddObjects(area.ObjectTemplates, areaPath + " / Templates", "TemplateObjectVariable");
                    AddObjects(area.GameObjects, areaPath + " / Game Objects", "GameObjectVariable");
                    AddRange(area.TraversalConnections.SelectMany(static connection => connection.Variables), areaPath + " / Traversal", "TraversalConnectionVariable");

                    foreach (var connection in area.TraversalConnections)
                    {
                        AddRange(connection.TraversalStateFromA.Variables, areaPath + " / TraversalLeg:A", "TraversalLegVariable");
                        AddRange(connection.TraversalStateFromB.Variables, areaPath + " / TraversalLeg:B", "TraversalLegVariable");
                    }

                    foreach (var room in area.Rooms)
                    {
                        var roomPath = areaPath + $" / {room.Name}";
                        AddRange(room.Variables, roomPath, "RoomVariable");
                        AddObjects(room.GameObjects, roomPath + " / Objects", "GameObjectVariable");
                    }
                }
            }
        }

        return map;
    }

    private static IReadOnlyList<ValidationSharedPropertyRelationship> BuildSharedRelationshipLookup(
        ProjectModel project,
        IReadOnlyDictionary<Guid, ValidationGamePropertyDescriptor> propertyById)
    {
        var relationships = new List<ValidationSharedPropertyRelationship>();

        foreach (var shared in project.SharedVariables)
        {
            var participants = shared.Participants
                .Where(static participant => participant.OwnerId != Guid.Empty)
                .ToList();

            if (participants.Count < 2)
            {
                continue;
            }

            for (var i = 0; i < participants.Count - 1; i++)
            {
                var source = participants[i];
                for (var j = i + 1; j < participants.Count; j++)
                {
                    var target = participants[j];
                    if (!propertyById.TryGetValue(source.OwnerId, out var sourceProperty)
                        || !propertyById.TryGetValue(target.OwnerId, out var targetProperty))
                    {
                        continue;
                    }

                    relationships.Add(new ValidationSharedPropertyRelationship(
                        source.OwnerId,
                        target.OwnerId,
                        "SharedVariable",
                        sourceProperty.ScopePath,
                        targetProperty.ScopePath));

                    relationships.Add(new ValidationSharedPropertyRelationship(
                        target.OwnerId,
                        source.OwnerId,
                        "SharedVariable",
                        targetProperty.ScopePath,
                        sourceProperty.ScopePath));
                }
            }
        }

        return relationships;
    }
}
