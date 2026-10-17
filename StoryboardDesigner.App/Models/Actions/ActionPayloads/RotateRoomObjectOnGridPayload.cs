using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameServices.References;
using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Models;

public sealed record RotateRoomObjectOnGridPayload : IActionPayload
{
    public RotateRoomObjectOnGridPayload(
        RuntimeRotateRoomObjectOnGridAttemptMode mode = RuntimeRotateRoomObjectOnGridAttemptMode.Turn,
        int? turnDegrees = null,
        string facingDirectionToken = "",
        string movementEffectKey = MovementPresentationEffectKeys.DefaultMedium,
        Guid? targetObjectId = null)
    {
        Mode = Enum.IsDefined(mode)
            ? mode
            : RuntimeRotateRoomObjectOnGridAttemptMode.Turn;
        TurnDegrees = turnDegrees;
        FacingDirectionToken = facingDirectionToken?.Trim() ?? string.Empty;
        MovementEffectKey = movementEffectKey?.Trim() ?? string.Empty;
        TargetObjectId = targetObjectId is Guid id && id != Guid.Empty ? id : null;
    }

    public CommandActionType ActionType => CommandActionType.RotateRoomObjectOnGrid;

    public RuntimeRotateRoomObjectOnGridAttemptMode Mode { get; init; }

    public int? TurnDegrees { get; init; }

    public string FacingDirectionToken { get; init; }

    public string MovementEffectKey { get; init; }

    public Guid? TargetObjectId { get; init; }

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
