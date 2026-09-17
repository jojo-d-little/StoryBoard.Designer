namespace StoryboardDesigner.App.Models;

public interface IActionLinkedActionsProvider
{
    IReadOnlyList<LinkedActionReference> GetLinkedActions()
    {
        return [];
    }
}
