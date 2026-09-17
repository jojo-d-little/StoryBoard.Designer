using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class TraversalDirectionalTargetUniquenessRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Area };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "TRV-007",
        Title: "Traversal Directional Target Uniqueness",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Traversal");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not Area area)
        {
            yield break;
        }

        var areaPath = BuildAreaPath(context.Project, area);
        var roomNameById = area.Rooms.ToDictionary(room => room.Id, room => room.Name ?? string.Empty);

        var targetsBySourceAndDirection = new Dictionary<(Guid SourceRoomId, Direction10 Direction), HashSet<Guid>>();

        foreach (var connection in area.TraversalConnections)
        {
            foreach (var leg in EnumerateEffectiveLegs(connection))
            {
                if (leg.SourceRoomId == Guid.Empty || leg.DestinationRoomId == Guid.Empty)
                {
                    continue;
                }

                var key = (leg.SourceRoomId, leg.Direction);
                if (!targetsBySourceAndDirection.TryGetValue(key, out var targets))
                {
                    targets = new HashSet<Guid>();
                    targetsBySourceAndDirection[key] = targets;
                }

                targets.Add(leg.DestinationRoomId);
            }
        }

        foreach (var entry in targetsBySourceAndDirection)
        {
            if (entry.Value.Count <= 1)
            {
                continue;
            }

            var sourceRoomName = roomNameById.TryGetValue(entry.Key.SourceRoomId, out var resolvedSource)
                ? resolvedSource
                : entry.Key.SourceRoomId.ToString("N");
            var targetNames = entry.Value
                .Select(targetId => roomNameById.TryGetValue(targetId, out var roomName) ? roomName : targetId.ToString("N"))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                $"{areaPath} / Room/{sourceRoomName}",
                $"room '{sourceRoomName}' has multiple traversal targets for direction {entry.Key.Direction}: {string.Join(", ", targetNames)}.",
                "Keep one effective traversal target per room and direction, or use a different direction for alternate routes.");
        }
    }

    private static IEnumerable<(Guid SourceRoomId, Guid DestinationRoomId, Direction10 Direction)> EnumerateEffectiveLegs(TraversalConnection connection)
    {
        if (connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayAtoB)
        {
            yield return (connection.RoomAId, connection.RoomBId, connection.BaseTraversalDirectionFromA);
        }

        if (connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayBtoA)
        {
            yield return (connection.RoomBId, connection.RoomAId, TraversalWizardApplyUtility.OppositeDirection(connection.BaseTraversalDirectionFromA));
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
