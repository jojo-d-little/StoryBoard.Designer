using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class AdjacentRoomDuplicateNormalizedNameWarningRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Area
        };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "NAV-001",
        Title: "Duplicate Normalized Room Names In Area",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Navigation");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not Area area)
        {
            yield break;
        }

        var duplicateGroups = area.Rooms
            .Select(room => new
            {
                NormalizedName = NormalizeRoomName(room.Name),
                RoomName = room.Name?.Trim() ?? string.Empty
            })
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.NormalizedName))
            .GroupBy(static entry => entry.NormalizedName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        if (duplicateGroups.Count == 0)
        {
            yield break;
        }

        var path = BuildAreaPath(area, context.Project);
        foreach (var group in duplicateGroups)
        {
            var roomNames = group
                .Select(static entry => entry.RoomName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                path,
                $"Duplicate normalized room name '{group.Key}' in area '{area.Name}' can make adjacent destination matching ambiguous. Rooms: {string.Join(", ", roomNames)}.");
        }
    }

    private static string NormalizeRoomName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var parts = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }

    private static string BuildAreaPath(Area area, ProjectModel project)
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
