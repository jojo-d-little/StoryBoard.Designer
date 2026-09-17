using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class CountryNodeViewModel : HierarchyNodeViewModel
{
    public CountryNodeViewModel(Country country, HierarchyNodeViewModel parent)
        : base(country.Name, parent)
    {
        Country = country;
        NodeTypeLabel = "Country";
    }

    public Country Country { get; }
    public bool IsStartingCountry => Parent is PlanetNodeViewModel planetNode
                                      && string.Equals(planetNode.Planet.StartingCountryName, Country.Name, StringComparison.OrdinalIgnoreCase);

    public void RefreshStartIndicator()
    {
        OnPropertyChanged(nameof(IsStartingCountry));
    }

    protected override void RenameModel(string newName)
    {
        var oldName = Country.Name;
        Country.Name = newName;

        if (Parent is PlanetNodeViewModel planetNode
            && string.Equals(planetNode.Planet.StartingCountryName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            planetNode.Planet.StartingCountryName = newName;
        }

        OnPropertyChanged(nameof(IsStartingCountry));
    }
}
