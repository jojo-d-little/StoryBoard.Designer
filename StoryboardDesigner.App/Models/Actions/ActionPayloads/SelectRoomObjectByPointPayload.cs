namespace StoryboardDesigner.App.Models;

public sealed record SelectRoomObjectByPointPayload(string SelectionCueEffectKey) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.SelectRoomObjectByPoint;
}
