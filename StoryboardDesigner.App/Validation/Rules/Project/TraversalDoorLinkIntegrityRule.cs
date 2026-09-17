using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class TraversalDoorLinkIntegrityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Area };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "TRV-004",
        Title: "Traversal Door Link Integrity",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Traversal");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not Area area)
        {
            yield break;
        }

        var roomById = area.Rooms.ToDictionary(room => room.Id, room => room);
        var areaPath = BuildAreaPath(context.Project, area);

        foreach (var connection in area.TraversalConnections)
        {
            var connectionPath = $"{areaPath} / TraversalConnection/{connection.TraversalConnectionId:N}";

            foreach (var issue in ValidateLeg(
                         roomById,
                         connection.RoomAId,
                         connection.TraversalStateFromA,
                         connectionPath + " / LegAtoB / DoorLink"))
            {
                yield return issue;
            }

            foreach (var issue in ValidateLeg(
                         roomById,
                         connection.RoomBId,
                         connection.TraversalStateFromB,
                         connectionPath + " / LegBtoA / DoorLink"))
            {
                yield return issue;
            }
        }
    }

    private IEnumerable<ValidationIssue> ValidateLeg(
        IReadOnlyDictionary<Guid, Room> roomById,
        Guid sourceRoomId,
        TraversalLegState legState,
        string doorLinkPath)
    {
        if (!roomById.TryGetValue(sourceRoomId, out var sourceRoom))
        {
            yield break;
        }

        if (!legState.OpenableObjectId.HasValue || legState.OpenableObjectId.Value == Guid.Empty)
        {
            yield break;
        }

        var passableVariable = legState.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        if (passableVariable is null)
        {
            yield break;
        }

        var door = EnumerateObjects(sourceRoom.GameObjects)
            .FirstOrDefault(candidate => candidate.ObjectId == legState.OpenableObjectId.Value);
        if (door is null)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                doorLinkPath,
                "IsDoorLinkedToTraversal: traversal leg references a door that no longer exists in the source room.",
                "Choose a valid door in traversal settings or clear the door link, then relink.");
            yield break;
        }

        var doorOpenVariable = door.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        if (doorOpenVariable is null)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                doorLinkPath,
                "IsDoorLinkedToTraversal: linked door is missing required isOpen variable.",
                "Use Manage Traversals > Relink Doors to rebuild door/traversal shared linkage.");
            yield break;
        }

        var passableSharedId = passableVariable.SharedVariableId ?? legState.SharedVariableId;
        var doorSharedId = doorOpenVariable.SharedVariableId;
        if (!passableSharedId.HasValue
            || passableSharedId.Value == Guid.Empty
            || !doorSharedId.HasValue
            || doorSharedId.Value == Guid.Empty
            || passableSharedId.Value != doorSharedId.Value)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                doorLinkPath,
                "IsDoorLinkedToTraversal: door isOpen and traversal isPassable are not linked to the same shared variable.",
                "Use Manage Traversals > Relink Doors to repair the shared linkage.");
        }
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

    private static string BuildAreaPath(ProjectModel project, Area area)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                if (country.Areas.Any(candidate => ReferenceEquals(candidate, area) || candidate.Id == area.Id))
                {
                    return $"Global / {planet.Name} / {country.Name} / {area.Name}";
                }
            }
        }

        return $"Global / {area.Name}";
    }
}
