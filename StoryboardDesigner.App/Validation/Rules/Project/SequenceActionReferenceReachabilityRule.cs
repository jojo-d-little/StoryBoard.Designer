using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

/// <summary>
/// Verifies that shared actions referenced by sequence steps exist and are available
/// from the sequence owner through the same scope paths offered by the sequence picker.
/// </summary>
public sealed class SequenceActionReferenceReachabilityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        Enum.GetValues<ScopeNodeKind>().ToHashSet();

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "SEQ-003",
        Title: "Sequence Action Reference Reachability",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Sequences");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not ScopeNodeBase ownerScope)
        {
            yield break;
        }

        if (ownerScope is GameObject { IsRoomInstance: true })
        {
            yield break;
        }

        var allActionLocations = context.SharedState.GetOrAdd(
            "SEQ:ActionLocationsById",
            () => BuildActionLocations(context.Project));
        var reachableScopes = BuildReachableScopes(ownerScope)
            .ToHashSet<IScopedAwareNode>(ReferenceEqualityComparer.Instance);
        var reachableActionIds = reachableScopes
            .SelectMany(scope => GetOwnedActions(scope))
            .Where(static action => action.Id != Guid.Empty)
            .Select(static action => action.Id)
            .ToHashSet();

        foreach (var sequence in ownerScope.SequenceDefinitions ?? new List<Storyboard.Shared.DesignerContracts.ProjectSequenceDefinitionDto>())
        {
            var sequenceName = string.IsNullOrWhiteSpace(sequence.DisplayName)
                ? "(unnamed sequence)"
                : sequence.DisplayName.Trim();
            var branchNames = (sequence.Branches ?? new List<Storyboard.Shared.DesignerContracts.ProjectSequenceBranchDto>())
                .Where(static branch => !string.IsNullOrWhiteSpace(branch.BranchId))
                .GroupBy(static branch => branch.BranchId, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => string.IsNullOrWhiteSpace(group.First().DisplayName)
                        ? group.Key
                        : group.First().DisplayName.Trim(),
                    StringComparer.Ordinal);

            var steps = sequence.Steps ?? new List<Storyboard.Shared.DesignerContracts.ProjectSequenceStepDto>();
            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                if (step.StepType != SequenceStepType.Action
                    || step.ActionId is not Guid actionId
                    || actionId == Guid.Empty)
                {
                    continue;
                }

                var branchName = !string.IsNullOrWhiteSpace(step.BranchId)
                    && branchNames.TryGetValue(step.BranchId, out var displayBranchName)
                        ? displayBranchName
                        : string.IsNullOrWhiteSpace(step.BranchId) ? "(unnamed branch)" : step.BranchId;
                var path = $"{BuildScopePath(ownerScope)} / Sequence '{sequenceName}' / Branch '{branchName}' / Step {index + 1} ({step.StepId:D})";

                if (!allActionLocations.TryGetValue(actionId, out var locations) || locations.Count == 0)
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        path,
                        $"sequence '{sequenceName}' branch '{branchName}' step {index + 1} references action ID '{actionId:D}', which is not defined in the project.",
                        "Choose an existing action from this scope, an ancestor, or a permitted child scope, or use an inline action.");
                    continue;
                }

                if (reachableActionIds.Contains(actionId))
                {
                    continue;
                }

                var actionName = string.IsNullOrWhiteSpace(locations[0].Action.Name)
                    ? "(unnamed action)"
                    : locations[0].Action.Name.Trim();
                var definedAt = string.Join(", ", locations.Select(static location => $"'{location.ScopePath}'"));
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    path,
                    $"sequence '{sequenceName}' branch '{branchName}' step {index + 1} references action '{actionName}' ({actionId:D}) defined at {definedAt}, outside the scopes reachable from '{BuildScopePath(ownerScope)}'.",
                    "Choose an action from this scope, an ancestor, or a permitted child scope, or move the action into a reachable scope.");
            }
        }
    }

    private static Dictionary<Guid, List<ActionLocation>> BuildActionLocations(ProjectModel project)
    {
        var result = new Dictionary<Guid, List<ActionLocation>>();
        foreach (var scope in EnumerateScopes(project))
        {
            var scopePath = BuildScopePath(scope);
            foreach (var action in GetOwnedActions(scope))
            {
                if (action.Id == Guid.Empty)
                {
                    continue;
                }

                if (!result.TryGetValue(action.Id, out var locations))
                {
                    locations = new List<ActionLocation>();
                    result.Add(action.Id, locations);
                }

                locations.Add(new ActionLocation(action, scopePath));
            }
        }

        return result;
    }

    private static IEnumerable<IScopedAwareNode> BuildReachableScopes(ScopeNodeBase ownerScope)
    {
        for (IScopedAwareNode? ancestor = ownerScope; ancestor is not null; ancestor = ancestor.ParentScope)
        {
            yield return ancestor;
        }

        var pending = new Stack<IScopedAwareNode>(ownerScope.ChildScopes.Reverse());
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;
            foreach (var child in current.ChildScopes.Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static IEnumerable<ScopeNodeBase> EnumerateScopes(IScopedAwareNode root)
    {
        var pending = new Stack<IScopedAwareNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is ScopeNodeBase scope)
            {
                yield return scope;
            }

            foreach (var child in current.ChildScopes.Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static IEnumerable<CommandAction> GetOwnedActions(IScopedAwareNode scope)
    {
        return scope switch
        {
            ProjectModel project => project.GlobalScopeAvailableActions,
            PhaseNode phase => phase.AvailableActions,
            GameObject gameObject => gameObject.AvailableActions,
            Room room => room.AvailableActions,
            Planet planet => planet.AvailableActions,
            Country country => country.AvailableActions,
            Area area => area.AvailableActions,
            _ => Array.Empty<CommandAction>()
        };
    }

    private static string BuildScopePath(IScopedAwareNode scope)
    {
        return string.Join(" / ", scope
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
    }

    private sealed record ActionLocation(CommandAction Action, string ScopePath);
}
