using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class SetPhaseTargetExistsRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-023",
        "Set Phase Target Exists",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context)
            || context.Action.ActionType != CommandActionType.SetPhase)
        {
            yield break;
        }

        var phaseKey = ActionPayloadAccessors.GetSetPhaseKey(context.Action);
        var message = string.IsNullOrWhiteSpace(phaseKey)
            ? "Choose a destination Page for this SetPhase action."
            : PhasePageTargetChoiceService.CountMatches(ruleContext.Project, phaseKey) switch
            {
                0 => $"SetPhase target '{phaseKey}' does not resolve to a Page.",
                > 1 => $"SetPhase target '{phaseKey}' is ambiguous because it matches more than one Page.",
                _ => null
            };

        if (message is null)
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
