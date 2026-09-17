namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomTraversalLegsNodeViewModel : HierarchyNodeViewModel
{
    public RoomTraversalLegsNodeViewModel(RoomNodeViewModel roomNode)
        : base("Traversal Legs", roomNode)
    {
        RoomNode = roomNode;
        NodeTypeLabel = "Traversal Legs";
    }

    public RoomNodeViewModel RoomNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
