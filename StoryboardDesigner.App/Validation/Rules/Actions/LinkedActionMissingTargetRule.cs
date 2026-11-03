using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class LinkedActionMissingTargetRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-005", "Linked Action Missing Target", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        var ownerScope = ((ValidationActionCandidateNode)ruleContext.CandidateNode!).OwnerScope;
        if (action.ActionType == CommandActionType.LinkedActions)
        {
            var nodesById = action.LinkedFlowDefinition.Nodes
                .Where(static node => node.NodeId > 0)
                .GroupBy(static node => node.NodeId)
                .ToDictionary(static group => group.Key, static group => group.First());
            foreach (var node in nodesById.Values)
            {
                if (!IsLinkTargetFound(context, node.ActionId, node.TargetObjectId))
                {
                    var lookupFailure = node.TargetObjectId is Guid targetObjectId
                        ? $"on explicit target object '{targetObjectId}'"
                        : "in its eligible lookup scopes";
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                        $"linked action '{action.Name}' references action id '{node.ActionId}', which was not found {lookupFailure}. "
                        + ActionRuleSupport.DescribeMissingLinkTarget(ruleContext.Project, context, ownerScope, node.ActionId, node.TargetObjectId));
                }

                foreach (var link in node.Links.Where(link => !nodesById.ContainsKey(link.TargetNodeId)))
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                        $"linked action '{action.Name}' node {node.NodeId} targets missing flow node {link.TargetNodeId}.");
                }
            }

            yield break;
        }

        foreach (var link in action.GetLinkedActions())
        {
            if (context.LinkTargetById.ContainsKey(link.ActionId))
            {
                continue;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                $"action '{action.Name}' links to action id '{link.ActionId}', which was not found in its eligible lookup scopes. "
                + ActionRuleSupport.DescribeMissingLinkTarget(ruleContext.Project, context, ownerScope, link.ActionId, targetObjectId: null));
        }
    }

    private static bool IsLinkTargetFound(
        CandidateActionValidationContext context,
        Guid actionId,
        Guid? targetObjectId)
    {
        if (targetObjectId is Guid explicitObjectId)
        {
            return context.ActionsByObjectId.TryGetValue(explicitObjectId, out var objectActions)
                && objectActions.ContainsKey(actionId);
        }

        return context.LinkTargetById.ContainsKey(actionId);
    }

}
