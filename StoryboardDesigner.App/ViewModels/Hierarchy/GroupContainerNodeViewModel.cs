namespace StoryboardDesigner.App.ViewModels;

public abstract class GroupContainerNodeViewModel : HierarchyNodeViewModel
{
    protected GroupContainerNodeViewModel(string name, HierarchyNodeViewModel ownerNode)
        : base(name, ownerNode)
    {
        OwnerNode = ownerNode;
    }

    public HierarchyNodeViewModel OwnerNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
