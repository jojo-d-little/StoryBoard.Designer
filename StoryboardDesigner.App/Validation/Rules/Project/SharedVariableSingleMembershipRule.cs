using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SharedVariableSingleMembershipRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-003",
        Title: "Shared Variable Single Membership",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var project = context.Project;
        var ownerPathByVariableId = BuildVariableOwnerPathLookup(project);
        var sharedVariablesById = project.SharedVariables
            .Where(static shared => shared.Id != Guid.Empty)
            .ToDictionary(shared => shared.Id);
        var membershipsByVariable = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var shared in sharedVariablesById.Values)
        {
            foreach (var participant in shared.Participants)
            {
                if (participant.OwnerId == Guid.Empty)
                {
                    continue;
                }

                if (!membershipsByVariable.TryGetValue(participant.OwnerId, out var memberships))
                {
                    memberships = new HashSet<Guid>();
                    membershipsByVariable[participant.OwnerId] = memberships;
                }

                memberships.Add(shared.Id);
            }
        }

        foreach (var entry in membershipsByVariable.Where(static pair => pair.Value.Count > 1))
        {
            var variableId = entry.Key;
            var sharedMembers = entry.Value
                .OrderBy(id => id)
                .Select(id =>
                {
                    if (sharedVariablesById.TryGetValue(id, out var shared) && !string.IsNullOrWhiteSpace(shared.Name))
                    {
                        return $"{shared.Name} ({id:N})";
                    }

                    return id.ToString("N");
                })
                .ToList();

            var issuePath = "Project";
            var variableLabel = variableId.ToString("N");
            var ownerPath = "Project";
            if (context.Lookup.TryGetPropertyByVariableId(variableId, out var property))
            {
                issuePath = string.IsNullOrWhiteSpace(property.ScopePath) ? "Project" : property.ScopePath;
                variableLabel = string.IsNullOrWhiteSpace(property.VariableName) ? variableLabel : property.VariableName;
                ownerPath = issuePath;
            }

            if (ownerPathByVariableId.TryGetValue(variableId, out var resolvedOwnerPath)
                && !string.IsNullOrWhiteSpace(resolvedOwnerPath))
            {
                ownerPath = resolvedOwnerPath;
                issuePath = resolvedOwnerPath;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                issuePath,
                $"variable '{variableLabel}' ({variableId:N}) owned by '{ownerPath}' participates in multiple shared variables ({string.Join(", ", sharedMembers)}). A variable may belong to at most one shared variable.");
        }
    }

    private static Dictionary<Guid, string> BuildVariableOwnerPathLookup(ProjectModel project)
    {
        var map = new Dictionary<Guid, string>();

        static string Normalize(string? value, string fallback)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        static void AddPath(Dictionary<Guid, string> targetMap, IEnumerable<GamePropertyDefinition> variables, string ownerPath)
        {
            foreach (var variable in variables)
            {
                if (variable.Id == Guid.Empty)
                {
                    continue;
                }

                targetMap[variable.Id] = ownerPath;
            }
        }

        static void AddObjectPaths(Dictionary<Guid, string> targetMap, IEnumerable<GameObject> objects, string basePath)
        {
            foreach (var gameObject in objects)
            {
                var objectName = Normalize(gameObject.Name, "GameObject");
                var objectPath = $"{basePath} / {objectName}";
                AddPath(targetMap, gameObject.Variables, objectPath);
                AddObjectPaths(targetMap, gameObject.ContainedObjects, objectPath);
            }
        }

        AddPath(map, project.GlobalVariables, "Global");
        AddPath(map, project.GlobalScope.GameProperties, "Global / Game Properties");
        AddObjectPaths(map, project.GlobalScope.GameObjects, "Global");
        AddObjectPaths(map, project.ObjectTemplates, "Global / Templates");

        foreach (var planet in project.Planets)
        {
            var planetPath = $"Global / {Normalize(planet.Name, "Planet")}";
            AddPath(map, planet.Variables, planetPath);
            AddObjectPaths(map, planet.GameObjects, planetPath);

            foreach (var country in planet.Countries)
            {
                var countryPath = $"{planetPath} / {Normalize(country.Name, "Country")}";
                AddPath(map, country.Variables, countryPath);
                AddObjectPaths(map, country.GameObjects, countryPath);

                foreach (var area in country.Areas)
                {
                    var areaPath = $"{countryPath} / {Normalize(area.Name, "Area")}";
                    AddPath(map, area.Variables, areaPath);
                    AddObjectPaths(map, area.GameObjects, areaPath);
                    AddPath(map, area.TraversalConnections.SelectMany(static connection => connection.Variables), areaPath + " / Traversal");

                    foreach (var connection in area.TraversalConnections)
                    {
                        AddPath(map, connection.TraversalStateFromA.Variables, areaPath + " / TraversalLeg:A");
                        AddPath(map, connection.TraversalStateFromB.Variables, areaPath + " / TraversalLeg:B");
                    }

                    foreach (var room in area.Rooms)
                    {
                        var roomPath = $"{areaPath} / {Normalize(room.Name, "Room")}";
                        AddPath(map, room.Variables, roomPath);
                        AddObjectPaths(map, room.GameObjects, roomPath);
                    }
                }
            }
        }

        return map;
    }
}
