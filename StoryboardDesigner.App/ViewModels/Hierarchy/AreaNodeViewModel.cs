using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class AreaNodeViewModel : HierarchyNodeViewModel
{
    public AreaNodeViewModel(Area area, HierarchyNodeViewModel parent)
        : base(area.Name, parent)
    {
        Area = area;
        NodeTypeLabel = "Area";
    }

    public Area Area { get; }
    public bool IsStartingArea => Parent is CountryNodeViewModel countryNode
                                  && string.Equals(countryNode.Country.StartingAreaName, Area.Name, StringComparison.OrdinalIgnoreCase);

    public void RefreshStartIndicator()
    {
        OnPropertyChanged(nameof(IsStartingArea));
    }

    protected override void RenameModel(string newName)
    {
        var oldName = Area.Name;
        Area.Name = newName;

        if (Parent is CountryNodeViewModel countryNode
            && string.Equals(countryNode.Country.StartingAreaName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            countryNode.Country.StartingAreaName = newName;
        }

        OnPropertyChanged(nameof(IsStartingArea));
    }
}
