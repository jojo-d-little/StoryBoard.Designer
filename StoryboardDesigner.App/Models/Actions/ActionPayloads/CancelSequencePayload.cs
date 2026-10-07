namespace StoryboardDesigner.App.Models;

public sealed record CancelSequencePayload(Guid SequenceId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.CancelSequence;
}
