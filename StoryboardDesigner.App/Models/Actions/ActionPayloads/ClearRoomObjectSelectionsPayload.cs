namespace StoryboardDesigner.App.Models;

public sealed record ClearRoomObjectSelectionsPayload(ClearRoomObjectSelectionsScope ClearScope) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.ClearRoomObjectSelections;
}
