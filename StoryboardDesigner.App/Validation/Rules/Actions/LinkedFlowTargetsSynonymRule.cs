using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class LinkedFlowTargetsSynonymRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-006", "Linked Flow Targets Synonym", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        foreach (var link in action.GetLinkedActions())
        {
            if (!context.LinkTargetById.TryGetValue(link.ActionId, out var target)
                || target.ActionType != CommandActionType.Synonym)
            {
                continue;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                $"linked flow cannot target synonym action '{target.Name}'.");
        }
    }
}
