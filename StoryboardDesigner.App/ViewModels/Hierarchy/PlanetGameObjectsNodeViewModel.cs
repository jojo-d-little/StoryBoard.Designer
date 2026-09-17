using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class PlanetGameObjectsNodeViewModel : GameObjectsNodeViewModel
{
    public PlanetGameObjectsNodeViewModel(PlanetNodeViewModel planetNode)
        : base("Game Objects", planetNode)
    {
        PlanetNode = planetNode;
    }

    public PlanetNodeViewModel PlanetNode { get; }
    public override List<GameObject> GameObjects => PlanetNode.Planet.GameObjects;
    public override IScopedAwareNode ScopeNode => PlanetNode.Planet;
}
