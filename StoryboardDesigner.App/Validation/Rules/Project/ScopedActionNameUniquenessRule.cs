using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class ScopedActionNameUniquenessRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Global,
            ScopeNodeKind.Planet,
            ScopeNodeKind.Country,
            ScopeNodeKind.Area,
            ScopeNodeKind.Room,
            ScopeNodeKind.GameObject
        };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-016",
        Title: "Scoped Action Name Uniqueness",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = context.CandidateNode;
        var actions = GetScopedActions(scope);
        var duplicateActionNameGroups = actions
            .Select(static action => action.Name?.Trim() ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateActionNameGroups.Count == 0)
        {
            yield break;
        }

        var scopePath = BuildScopePath(scope);
        foreach (var duplicateGroup in duplicateActionNameGroups)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                scopePath,
                $"Duplicate action name '{duplicateGroup.Key}' in scope. Action names must be unique within a scope for deterministic event target resolution.");
        }
    }

    private static IReadOnlyList<CommandAction> GetScopedActions(ScopeNodeBase scope)
    {
        return scope switch
        {
            ProjectModel project => project.GlobalScope.AvailableActions,
            Planet planet => planet.AvailableActions,
            Country country => country.AvailableActions,
            Area area => area.AvailableActions,
            Room room => room.AvailableActions,
            GameObject gameObject => gameObject.AvailableActions,
            _ => []
        };
    }

    private static string BuildScopePath(ScopeNodeBase node)
    {
        var scopes = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name));

        return string.Join(" / ", scopes);
    }
}