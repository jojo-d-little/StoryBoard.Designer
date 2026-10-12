using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GamePropertiesContainerNodeViewModel : HierarchyNodeViewModel
{
    public GamePropertiesContainerNodeViewModel(
        PropertyResolutionScope scope,
        ScopeNodeBase scopeOwner,
        List<GamePropertyDefinition> variables,
        string ownerDisplayName,
        HierarchyNodeViewModel? parent = null,
        TraversalLegNodeViewModel? traversalLegOwner = null)
        : base("Game Properties", parent)
    {
        Scope = scope;
        ScopeOwner = scopeOwner;
        Variables = variables;
        OwnerDisplayName = ownerDisplayName;
        TraversalLegOwner = traversalLegOwner;
        NodeTypeLabel = "Game Properties";
    }

    public PropertyResolutionScope Scope { get; }
    public List<GamePropertyDefinition> Variables { get; }
    public string OwnerDisplayName { get; }
    public TraversalLegNodeViewModel? TraversalLegOwner { get; }

    protected override void RenameModel(string newName)
    {
    }
}
