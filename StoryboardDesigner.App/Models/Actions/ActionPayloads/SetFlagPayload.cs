namespace StoryboardDesigner.App.Models;

public sealed record SetFlagPayload(string FlagName, bool FlagValue) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.SetFlag;
}
