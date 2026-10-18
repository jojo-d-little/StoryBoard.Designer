using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class MovementPresentationEffectKeyRule : IValidationRule
{
    private readonly PresentationEffectsCatalogService _catalogService = new();

    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-022",
        "Movement Presentation Effect",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var effectKey = context.Action.ActionType switch
        {
            CommandActionType.MoveRoomObjectOnGrid => ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(context.Action).MovementEffectKey,
            CommandActionType.MoveRoomObjectByPoints => ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(context.Action).MovementEffectKey,
            CommandActionType.RotateRoomObjectOnGrid => ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(context.Action).MovementEffectKey,
            CommandActionType.StackRoomObjectOnAnother => ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(context.Action).MovementEffectKey,
            _ => null
        };

        if (effectKey is null || _catalogService.IsConfiguredMovementEffectKey(effectKey))
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, context.Action),
            $"Action '{context.Action.Name}' must use an effect key from the Movement category in the Configuration catalog.");
    }
}
