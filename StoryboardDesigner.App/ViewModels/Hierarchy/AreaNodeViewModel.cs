using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class AreaNodeViewModel : HierarchyNodeViewModel
{
    public AreaNodeViewModel(Area area, HierarchyNodeViewModel parent)
        : base(area.Name, parent)
    {
        Area = area;
        ScopeOwner = area;
        NodeTypeLabel = "Area";
    }

    public Area Area { get; }
    public bool IsStartingArea => Area.ParentScope is Country country
                                  && string.Equals(country.StartingAreaName, Area.Name, StringComparison.OrdinalIgnoreCase);

    public void RefreshStartIndicator()
    {
        OnPropertyChanged(nameof(IsStartingArea));
    }

    protected override void RenameModel(string newName)
    {
        var oldName = Area.Name;
        Area.Name = newName;

        if (Area.ParentScope is Country country
            && string.Equals(country.StartingAreaName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            country.StartingAreaName = newName;
        }

        OnPropertyChanged(nameof(IsStartingArea));
    }
}
