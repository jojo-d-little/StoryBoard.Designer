using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

/// <summary>A visual grouping for authored constructs owned by a scope.</summary>
public sealed class ConstructsNodeViewModel : GroupContainerNodeViewModel
{
    public ConstructsNodeViewModel(HierarchyNodeViewModel ownerNode, ScopeNodeBase scopeOwner)
        : base("Constructs", ownerNode)
    {
        ScopeOwner = scopeOwner ?? throw new ArgumentNullException(nameof(scopeOwner));
        NodeTypeLabel = "Constructs";
    }
}
