using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class RoomPlacementReferenceIntegrityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Area };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "TRV-006",
        Title: "Room Placement Reference Integrity",
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
        var placedRoomIds = new HashSet<Guid>();

        for (var i = 0; i < area.RoomPlacements.Count; i++)
        {
            var placement = area.RoomPlacements[i];
            var placementPath = $"{areaPath} / RoomPlacement[{i + 1}]";

            if (placement.RoomId == Guid.Empty)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    placementPath,
                    "room placement references an empty room id.",
                    "Assign a room that exists in this area.");
                continue;
            }

            if (!roomIds.Contains(placement.RoomId))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    placementPath,
                    $"room placement references room id '{placement.RoomId:N}' that is not part of this area.",
                    "Select a room that belongs to this area or remove the stale placement entry.");
            }

            if (!placedRoomIds.Add(placement.RoomId))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    placementPath,
                    $"duplicate room placement exists for room id '{placement.RoomId:N}'.",
                    "Keep one room placement per room in an area.");
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