using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class LinkedActionSelfTargetRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-004", "Linked Action Self Target", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        var selfTargets = action.ActionType == CommandActionType.LinkedActions
            ? action.LinkedFlowDefinition.Nodes.Any(node => node.ActionId == action.Id)
            : action.GetLinkedActions().Any(link => link.ActionId == action.Id);
        if (selfTargets)
        {
            yield return new ValidationIssue(Metadata.RuleId, Metadata.DefaultSeverity, ActionRuleSupport.BuildActionIssuePath(context.Path, action), $"action '{action.Name}' cannot link to itself.");
        }
    }
}
