using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class CheckCommandContextPayloadRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-016",
        "Command Context Check Configuration",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context)
            || context.Action.ActionType != CommandActionType.CheckCommandContext)
        {
            yield break;
        }

        var payload = ActionPayloadAccessors.GetCheckCommandContext(context.Action);
        if (CommandContextPayloadValidator.TryValidate(payload.Field, payload.ExpectedBoolean, payload.ExpectedDirection, out var message))
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, context.Action),
            message);
    }
}
