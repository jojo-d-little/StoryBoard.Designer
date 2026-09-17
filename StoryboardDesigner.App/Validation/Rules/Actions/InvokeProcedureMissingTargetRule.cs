using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class InvokeProcedureMissingTargetRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-013", "InvokeProcedure Missing Target", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        if (action.ActionType != CommandActionType.InvokeProcedure)
        {
            yield break;
        }

        var procedureId = ActionPayloadAccessors.GetProcedureId(action);
        if (!procedureId.HasValue || procedureId.Value == Guid.Empty)
        {
            yield break;
        }

        var proceduresById = ruleContext.SharedState.GetOrAdd(
            "ACT:ProceduresById",
            () => ruleContext.Project.Procedures
                .Where(static procedure => procedure.Id != Guid.Empty)
                .GroupBy(procedure => procedure.Id)
                .ToDictionary(group => group.Key, group => group.First()));

        if (proceduresById.ContainsKey(procedureId.Value))
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            $"InvokeProcedure action '{action.Name}' references missing procedure id '{procedureId.Value:N}'.",
            "Select an existing procedure for this action in the action editor.");
    }
}