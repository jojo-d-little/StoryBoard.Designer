using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class TraversalLegPassableContractRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Area };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "TRV-003",
        Title: "Traversal Leg isPassable Contract",
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

            foreach (var issue in ValidateLeg(roomById, connection.RoomAId, connection.TraversalStateFromA, connectionPath + " / LegAtoB"))
            {
                yield return issue;
            }

            foreach (var issue in ValidateLeg(roomById, connection.RoomBId, connection.TraversalStateFromB, connectionPath + " / LegBtoA"))
            {
                yield return issue;
            }
        }
    }

    private IEnumerable<ValidationIssue> ValidateLeg(
        IReadOnlyDictionary<Guid, Room> roomById,
        Guid sourceRoomId,
        TraversalLegState legState,
        string legPath)
    {
        if (!roomById.ContainsKey(sourceRoomId))
        {
            yield break;
        }

        var passableVariable = legState.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));

        if (passableVariable is null)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                legPath,
                "traversal leg is missing required isPassable variable.",
                "Add a True/False isPassable variable to this traversal leg.");
            yield break;
        }

        if (passableVariable.ValueRestriction != GamePropertyValueRestriction.TrueFalse)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                legPath,
                "traversal leg isPassable variable must use TrueFalse restriction.",
                "Set isPassable value restriction to TrueFalse.");
        }

        if (!bool.TryParse(passableVariable.DefaultValue?.Trim(), out _))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                legPath,
                "traversal leg isPassable default value must be true or false.",
                "Set isPassable default value to true or false.");
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
