namespace StoryboardDesigner.App.ViewModels;

public sealed class GamePropertiesContainerNodeViewModel : HierarchyNodeViewModel
{
    public GamePropertiesContainerNodeViewModel(PropertyResolutionScope scope, HierarchyNodeViewModel? parent = null)
        : base("Game Properties", parent)
    {
        Scope = scope;
        NodeTypeLabel = "Game Properties";
    }

    public PropertyResolutionScope Scope { get; }

    protected override void RenameModel(string newName)
    {
    }
}
