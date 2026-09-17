using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class AreaGameObjectsNodeViewModel : GameObjectsNodeViewModel
{
    public AreaGameObjectsNodeViewModel(AreaNodeViewModel areaNode)
        : base("Game Objects", areaNode)
    {
        AreaNode = areaNode;
    }

    public AreaNodeViewModel AreaNode { get; }
    public override List<GameObject> GameObjects => AreaNode.Area.GameObjects;
    public override IScopedAwareNode ScopeNode => AreaNode.Area;
}
