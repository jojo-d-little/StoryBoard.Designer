namespace StoryboardDesigner.App.Models;

public sealed record MaterializeObjectCopyPayload(Guid? MaterializeSourceObjectId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.MaterializeObjectCopy;
}