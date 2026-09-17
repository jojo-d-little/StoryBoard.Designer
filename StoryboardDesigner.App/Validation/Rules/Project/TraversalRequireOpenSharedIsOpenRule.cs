using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class TraversalRequireOpenSharedIsOpenRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Area };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "TRV-001",
        Title: "Traversal RequireOpen Shared Link",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Traversal");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not Area area)
        {
            yield break;
        }

        var areaPath = BuildAreaPath(context.Project, area);

        foreach (var connection in area.TraversalConnections)
        {
            var connectionPath = $"{areaPath} / TraversalConnection/{connection.TraversalConnectionId:N}";

            foreach (var issue in ValidateLeg(
                         context.Project,
                         connection.TraversalStateFromA,
                         connectionPath + " / LegAtoB"))
            {
                yield return issue;
            }

            foreach (var issue in ValidateLeg(
                         context.Project,
                         connection.TraversalStateFromB,
                         connectionPath + " / LegBtoA"))
            {
                yield return issue;
            }
        }
    }

    private IEnumerable<ValidationIssue> ValidateLeg(ProjectModel project, TraversalLegState legState, string legPath)
    {
        if (legState.OpenStatePolicy != OpenablePolicy.RequireOpen)
        {
            yield break;
        }

        var passable = legState.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        if (passable is null)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                legPath,
                "traversal leg requires open state, but isPassable is missing.");
            yield break;
        }

        var sharedVariableId = passable.SharedVariableId ?? legState.SharedVariableId;
        if (!sharedVariableId.HasValue || sharedVariableId.Value == Guid.Empty)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                legPath,
                "traversal leg requires open state, but isPassable is not shared with an object isOpen variable.");
            yield break;
        }

        if (!HasObjectIsOpenVariableSharedId(project, sharedVariableId.Value))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                legPath,
                $"traversal leg requires open state, but shared variable {sharedVariableId:N} is not linked to any object isOpen variable.");
        }
    }

    private static bool HasObjectIsOpenVariableSharedId(ProjectModel project, Guid sharedVariableId)
    {
        foreach (var gameObject in EnumerateAllGameObjects(project))
        {
            if (gameObject.Variables.Any(variable =>
                    string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase)
                    && variable.SharedVariableId.HasValue
                    && variable.SharedVariableId.Value == sharedVariableId))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<GameObject> EnumerateAllGameObjects(ProjectModel project)
    {
        foreach (var gameObject in EnumerateObjects(project.GlobalScope.GameObjects))
        {
            yield return gameObject;
        }

        foreach (var templateObject in EnumerateObjects(project.ObjectTemplates))
        {
            yield return templateObject;
        }

        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var room in area.Rooms)
                    {
                        foreach (var gameObject in EnumerateObjects(room.GameObjects))
                        {
                            yield return gameObject;
                        }
                    }
                }
            }
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
