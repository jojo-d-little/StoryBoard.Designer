using System;

namespace StoryboardDesigner.App.Models;

public sealed record SynonymPayload(Guid? SynonymTargetActionId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.Synonym;

    public IReadOnlyList<LinkedActionReference> GetLinkedActions()
    {
        if (!SynonymTargetActionId.HasValue)
        {
            return [];
        }

        return
        [
            new LinkedActionReference
            {
                ActionId = SynonymTargetActionId.Value,
                RunWhen = LinkedActionRunWhen.Always,
                Order = 0
            }
        ];
    }
}
