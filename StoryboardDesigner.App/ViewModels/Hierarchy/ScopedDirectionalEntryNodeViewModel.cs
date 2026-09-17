namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedDirectionalEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedDirectionalEntryNodeViewModel(string directional, ScopedDirectionalsNodeViewModel parent)
        : base(string.IsNullOrWhiteSpace(directional) ? "Directional" : directional, parent)
    {
        Directional = directional;
        ParentDirectionalsNode = parent;
        NodeTypeLabel = "Directional";
    }

    public string Directional { get; }
    public ScopedDirectionalsNodeViewModel ParentDirectionalsNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
