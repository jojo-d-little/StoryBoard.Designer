namespace StoryboardDesigner.App.Models;

public sealed record NavigateDirectionPreFlightCheckPayload : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.NavigateDirectionPreFlightCheck;
}
