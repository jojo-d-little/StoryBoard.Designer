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
        RuntimeMovementVisualTransitionHint visualTransitionHint = RuntimeMovementVisualTransitionHint.Medium)
    {
        Mode = Enum.IsDefined(mode)
            ? mode
            : RuntimeRotateRoomObjectOnGridAttemptMode.Turn;
        TurnDegrees = turnDegrees;
        FacingDirectionToken = facingDirectionToken?.Trim() ?? string.Empty;
        VisualTransitionHint = visualTransitionHint;
    }

    public CommandActionType ActionType => CommandActionType.RotateRoomObjectOnGrid;

    public RuntimeRotateRoomObjectOnGridAttemptMode Mode { get; init; }

    public int? TurnDegrees { get; init; }

    public string FacingDirectionToken { get; init; }

    public RuntimeMovementVisualTransitionHint VisualTransitionHint { get; init; }

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
