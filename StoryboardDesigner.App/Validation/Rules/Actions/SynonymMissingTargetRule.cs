using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class SynonymMissingTargetRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-003", "Synonym Missing Target", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        var synonymTargetId = action.GetSynonymTargetActionId();
        if (action.ActionType == CommandActionType.Synonym && synonymTargetId.HasValue)
        {
            if (context.ActionById.ContainsKey(synonymTargetId.Value))
            {
                yield break;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                $"synonym action '{action.Name}' targets missing action id '{synonymTargetId.Value}'.");
        }
    }
}
