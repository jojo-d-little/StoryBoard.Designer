using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class CountryGameObjectsNodeViewModel : GameObjectsNodeViewModel
{
    public CountryGameObjectsNodeViewModel(CountryNodeViewModel countryNode)
        : base("Game Objects", countryNode)
    {
        CountryNode = countryNode;
        ScopeOwner = countryNode.Country;
    }

    public CountryNodeViewModel CountryNode { get; }
    public override List<GameObject> GameObjects => CountryNode.Country.GameObjects;
    public override IScopedAwareNode ScopeNode => CountryNode.Country;
}
