using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class TraversalDoorOpenStateLinkConventionRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "TRV-005",
        Title: "Door Open State Link Convention",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Traversal");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not GameObject door)
        {
            yield break;
        }

        if (!door.IsOpenable)
        {
            yield break;
        }

        var doorName = door.Name?.Trim() ?? string.Empty;
        if (doorName.IndexOf("door", StringComparison.OrdinalIgnoreCase) < 0)
        {
            yield break;
        }

        var sharedById = context.Project.SharedVariables
            .Where(static shared => shared.Id != Guid.Empty)
            .ToDictionary(shared => shared.Id);

        var doorPath = BuildObjectPath(context.Project, door);
        var openVariable = door.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase));

        if (openVariable is null)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                doorPath,
                "openable door convention: object is marked openable and named as a door, but isOpen variable is missing.",
                "Ensure the door has an isOpen variable and link it through a shared variable when needed.");
            yield break;
        }

        if (!openVariable.SharedVariableId.HasValue || openVariable.SharedVariableId.Value == Guid.Empty)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                doorPath,
                "openable door convention: door isOpen is not linked to anything via shared variable.",
                "Link this door's isOpen to a shared variable used by traversal isPassable.");
            yield break;
        }

        var sharedId = openVariable.SharedVariableId.Value;
        if (!sharedById.TryGetValue(sharedId, out var shared)
            || !HasExternalLink(shared, door.ObjectId))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                doorPath,
                $"openable door convention: door isOpen shared variable {sharedId:N} has no linked participants beyond this door.",
                "Attach at least one traversal leg isPassable (or other intended participant) to this shared variable.");
        }
    }

    private static bool HasExternalLink(SharedVariableDefinition shared, Guid doorObjectId)
    {
        return shared.Participants.Any(participant =>
            !(string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase)
              && participant.OwnerId == doorObjectId
              && string.Equals(participant.VariableName, "isOpen", StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<GameObject> EnumerateObjects(IEnumerable<GameObject> roots)
    {
        foreach (var root in roots)
        {
            if (root is null)
            {
                continue;
            }

            yield return root;
            foreach (var child in EnumerateObjects(root.ContainedObjects))
            {
                yield return child;
            }
        }
    }

    private static string BuildObjectPath(ProjectModel project, GameObject target)
    {
        foreach (var roomTemplate in project.RoomTemplates)
        {
            foreach (var templateRoomObject in EnumerateObjects(roomTemplate.GameObjects))
            {
                if (ReferenceEquals(templateRoomObject, target) || templateRoomObject.ObjectId == target.ObjectId)
                {
                    return $"Global / Room Templates / {roomTemplate.Name} / {target.Name}";
                }
            }
        }

        foreach (var playerObject in EnumerateObjects(project.GlobalScope.GameObjects))
        {
            if (ReferenceEquals(playerObject, target) || playerObject.ObjectId == target.ObjectId)
            {
                return $"Global / {target.Name}";
            }
        }

        foreach (var templateObject in EnumerateObjects(project.ObjectTemplates))
        {
            if (ReferenceEquals(templateObject, target) || templateObject.ObjectId == target.ObjectId)
            {
                return $"Global / Templates / {target.Name}";
            }
        }

        foreach (var baseObject in EnumerateObjects(project.BaseObjects))
        {
            if (ReferenceEquals(baseObject, target) || baseObject.ObjectId == target.ObjectId)
            {
                return $"Global / Base Objects / {target.Name}";
            }
        }

        foreach (var planet in project.Planets)
        {
            foreach (var planetBaseObject in EnumerateObjects(planet.BaseObjects))
            {
                if (ReferenceEquals(planetBaseObject, target) || planetBaseObject.ObjectId == target.ObjectId)
                {
                    return $"Global / {planet.Name} / Base Objects / {target.Name}";
                }
            }

            foreach (var country in planet.Countries)
            {
                foreach (var countryBaseObject in EnumerateObjects(country.BaseObjects))
                {
                    if (ReferenceEquals(countryBaseObject, target) || countryBaseObject.ObjectId == target.ObjectId)
                    {
                        return $"Global / {planet.Name} / {country.Name} / Base Objects / {target.Name}";
                    }
                }

                foreach (var area in country.Areas)
                {
                    foreach (var areaBaseObject in EnumerateObjects(area.BaseObjects))
                    {
                        if (ReferenceEquals(areaBaseObject, target) || areaBaseObject.ObjectId == target.ObjectId)
                        {
                            return $"Global / {planet.Name} / {country.Name} / {area.Name} / Base Objects / {target.Name}";
                        }
                    }

                    foreach (var room in area.Rooms)
                    {
                        foreach (var roomObject in EnumerateObjects(room.GameObjects))
                        {
                            if (ReferenceEquals(roomObject, target) || roomObject.ObjectId == target.ObjectId)
                            {
                                return $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name} / {target.Name}";
                            }
                        }
                    }
                }
            }
        }

        return $"Global / {target.Name}";
    }
}
