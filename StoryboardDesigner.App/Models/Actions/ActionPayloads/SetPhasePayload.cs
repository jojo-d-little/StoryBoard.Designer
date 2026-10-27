namespace StoryboardDesigner.App.Models;

public sealed record SetPhasePayload(string PhaseKey) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.SetPhase;
}
