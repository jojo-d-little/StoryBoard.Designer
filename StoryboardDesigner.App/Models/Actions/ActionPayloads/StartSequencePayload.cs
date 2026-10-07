namespace StoryboardDesigner.App.Models;

public sealed record StartSequencePayload(Guid SequenceId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.StartSequence;
}
