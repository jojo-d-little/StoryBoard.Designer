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
                $"action '{action.Name}' links to missing action id '{link.ActionId}'.");
        }
    }
}
