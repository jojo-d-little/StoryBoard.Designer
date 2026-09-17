namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedVerbEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedVerbEntryNodeViewModel(string verb, ScopedVerbsNodeViewModel parent)
        : base(string.IsNullOrWhiteSpace(verb) ? "Verb" : verb, parent)
    {
        Verb = verb;
        ParentVerbsNode = parent;
        NodeTypeLabel = "Verb";
    }

    public string Verb { get; }
    public ScopedVerbsNodeViewModel ParentVerbsNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
