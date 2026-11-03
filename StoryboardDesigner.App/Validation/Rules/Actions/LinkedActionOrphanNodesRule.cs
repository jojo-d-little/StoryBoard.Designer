using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class LinkedActionOrphanNodesRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-026", "Linked Action Disconnected Nodes", ValidationSeverity.Warning, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context)
            || context.Action.ActionType != CommandActionType.LinkedActions)
        {
            yield break;
        }

        var orphanedNodes = LinkedActionFlowReachability.Analyze(context.Action.LinkedFlowDefinition).OrphanedNodes;
        if (orphanedNodes.Count == 0)
        {
            yield break;
        }

        var details = orphanedNodes.Select(node => context.LinkTargetById.TryGetValue(node.ActionId, out var target)
            ? $"node {node.NodeId} action '{target.Name}' ({node.ActionId:N})"
            : $"node {node.NodeId} action id '{node.ActionId:N}' (missing action)");
        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, context.Action),
            $"disconnected Linked Action nodes are retained in the source model but excluded from saved and exported flows: {string.Join(", ", details)}.",
            "Reconnect these nodes or remove them in the Linked Action editor.");
    }
}
