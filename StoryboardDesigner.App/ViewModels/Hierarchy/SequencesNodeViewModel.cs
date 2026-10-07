using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class SequencesNodeViewModel : HierarchyNodeViewModel
{
    public SequencesNodeViewModel(ScopeNodeBase owner, HierarchyNodeViewModel parent)
        : base("Sequences", parent)
    {
        Owner = owner;
        NodeTypeLabel = "Sequences";
    }

    public ScopeNodeBase Owner { get; }
}
