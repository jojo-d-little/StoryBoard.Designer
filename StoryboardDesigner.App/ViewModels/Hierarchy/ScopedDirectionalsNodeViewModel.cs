namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedDirectionalsNodeViewModel : HierarchyNodeViewModel
{
    public ScopedDirectionalsNodeViewModel(PropertyResolutionScope scope, IList<string> directionals, HierarchyNodeViewModel parent)
        : base("Directionals", parent)
    {
        Scope = scope;
        Directionals = directionals;
        NodeTypeLabel = "Directionals";
    }

    public PropertyResolutionScope Scope { get; }
    public IList<string> Directionals { get; }

    protected override void RenameModel(string newName)
    {
    }
}
