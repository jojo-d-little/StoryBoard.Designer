namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedVerbsNodeViewModel : HierarchyNodeViewModel
{
    public ScopedVerbsNodeViewModel(PropertyResolutionScope scope, IList<string> verbs, HierarchyNodeViewModel parent)
        : base("Verbs", parent)
    {
        Scope = scope;
        Verbs = verbs;
        NodeTypeLabel = "Verbs";
    }

    public PropertyResolutionScope Scope { get; }
    public IList<string> Verbs { get; }

    protected override void RenameModel(string newName)
    {
    }
}
