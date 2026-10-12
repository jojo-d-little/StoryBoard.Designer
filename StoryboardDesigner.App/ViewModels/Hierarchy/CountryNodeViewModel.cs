using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class CountryNodeViewModel : HierarchyNodeViewModel
{
    public CountryNodeViewModel(Country country, HierarchyNodeViewModel parent)
        : base(country.Name, parent)
    {
        Country = country;
        ScopeOwner = country;
        NodeTypeLabel = "Country";
    }

    public Country Country { get; }
    public bool IsStartingCountry => Country.ParentScope is Planet planet
                                      && string.Equals(planet.StartingCountryName, Country.Name, StringComparison.OrdinalIgnoreCase);

    public void RefreshStartIndicator()
    {
        OnPropertyChanged(nameof(IsStartingCountry));
    }

    protected override void RenameModel(string newName)
    {
        var oldName = Country.Name;
        Country.Name = newName;

        if (Country.ParentScope is Planet planet
            && string.Equals(planet.StartingCountryName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            planet.StartingCountryName = newName;
        }

        OnPropertyChanged(nameof(IsStartingCountry));
    }
}
