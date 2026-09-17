using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class SynonymSelfTargetRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-002", "Synonym Self Target", ValidationSeverity.Error, "Actions");
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
        if (action.ActionType == CommandActionType.Synonym
            && synonymTargetId.HasValue
            && synonymTargetId.Value == action.Id)
        {
            yield return new ValidationIssue(Metadata.RuleId, Metadata.DefaultSeverity, ActionRuleSupport.BuildActionIssuePath(context.Path, action), $"synonym action '{action.Name}' cannot target itself.");
        }
    }
}
