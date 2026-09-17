using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public abstract class GameObjectsNodeViewModel : HierarchyNodeViewModel
{
    protected GameObjectsNodeViewModel(string displayName, HierarchyNodeViewModel parent)
        : base(displayName, parent)
    {
        NodeTypeLabel = "Game Objects";
    }

    public abstract List<GameObject> GameObjects { get; }
    public abstract IScopedAwareNode ScopeNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
