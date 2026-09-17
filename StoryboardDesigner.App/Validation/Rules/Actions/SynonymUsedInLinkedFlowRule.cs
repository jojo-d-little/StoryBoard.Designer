using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class SynonymUsedInLinkedFlowRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-007", "Synonym Used In Linked Flow", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        var linkedTargets = context.Actions
            .SelectMany(static actionNode => actionNode.GetLinkedActions())
            .Select(link => link.ActionId)
            .ToHashSet();

        if (action.ActionType == CommandActionType.Synonym && linkedTargets.Contains(action.Id))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                $"synonym action '{action.Name}' cannot be used in linked-action flow.");
        }
    }
}
