using System.Collections.Generic;

namespace StoryboardDesigner.App.Models;

public sealed record LinkedFlowPayload(IReadOnlyList<LinkedActionReference> LinkedActions) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.LinkedActions;

    public IReadOnlyList<LinkedActionReference> GetLinkedActions()
    {
        return LinkedActions;
    }
}
