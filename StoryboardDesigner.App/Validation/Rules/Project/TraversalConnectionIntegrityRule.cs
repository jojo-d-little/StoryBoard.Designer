using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class TraversalConnectionIntegrityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Area };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "TRV-002",
        Title: "Traversal Connection Integrity",
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
        var roomIds = area.Rooms.Select(room => room.Id).ToHashSet();
        var pairSet = new HashSet<(Guid A, Guid B)>();

        foreach (var connection in area.TraversalConnections)
        {
            var connectionPath = $"{areaPath} / TraversalConnection/{connection.TraversalConnectionId:N}";

            if (connection.RoomAId == Guid.Empty || connection.RoomBId == Guid.Empty)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    connectionPath,
                    "traversal connection references an empty room id.",
                    "Assign valid room ids for both RoomAId and RoomBId.");
                continue;
            }

            if (connection.RoomAId == connection.RoomBId)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    connectionPath,
                    "traversal connection cannot reference the same room on both sides.",
                    "Set RoomAId and RoomBId to different rooms.");
            }

            if (!roomIds.Contains(connection.RoomAId) || !roomIds.Contains(connection.RoomBId))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    connectionPath,
                    "traversal connection references room ids that are not part of this area.",
                    "Update room ids to rooms contained by this area or remove the invalid connection.");
            }

            var pair = ToPair(connection.RoomAId, connection.RoomBId);
            if (!pairSet.Add(pair))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    connectionPath,
                    "duplicate traversal connection exists for this room pair.",
                    "Keep a single traversal connection per unordered room pair.");
            }

            if (connection.TraversalModeOverride == AreaAdjacencyMode.FourDirectional && IsDiagonal(connection.BaseTraversalDirectionFromA))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    connectionPath,
                    "base traversal direction is diagonal while traversal mode override is FourDirectional.",
                    "Use a cardinal base traversal direction or change traversal mode override to EightDirectional.");
            }
        }
    }

    private static (Guid A, Guid B) ToPair(Guid first, Guid second)
    {
        return first.CompareTo(second) <= 0 ? (first, second) : (second, first);
    }

    private static bool IsDiagonal(Direction10 direction)
    {
        return direction is Direction10.NorthEast or Direction10.SouthEast or Direction10.SouthWest or Direction10.NorthWest;
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
