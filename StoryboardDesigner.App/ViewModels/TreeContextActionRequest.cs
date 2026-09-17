namespace StoryboardDesigner.App.ViewModels;

public sealed class TreeContextActionRequest
{
    public TreeContextActionRequest(string actionId, HierarchyNodeViewModel node)
    {
        ActionId = actionId;
        Node = node;
    }

    public string ActionId { get; }

    public HierarchyNodeViewModel Node { get; }
}
