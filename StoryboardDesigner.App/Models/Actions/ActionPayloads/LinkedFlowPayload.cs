using System.Collections.Generic;

namespace StoryboardDesigner.App.Models;

public sealed record LinkedFlowPayload(LinkedActionFlowDefinition Definition) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.LinkedActions;

    public IReadOnlyList<LinkedActionReference> GetLinkedActions()
    {
        return Definition.GetEntryLinks();
    }
}
