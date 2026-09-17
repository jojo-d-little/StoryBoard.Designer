namespace StoryboardDesigner.App.Models;

public sealed record CheckGamePropertyPayload(string PropertyName, bool ExpectedValue) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.CheckGameProperty;
}
