using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class PlanetNodeViewModel : HierarchyNodeViewModel
{
    private readonly ProjectModel _project;

    public PlanetNodeViewModel(ProjectModel project, Planet planet, HierarchyNodeViewModel? parent = null) : base(planet.Name, parent)
    {
        _project = project;
        Planet = planet;
        NodeTypeLabel = "Planet";
    }

    public Planet Planet { get; }
    private bool _isStartingPlanet;
    public bool IsStartingPlanet
    {
        get => _isStartingPlanet;
        private set
        {
            if (_isStartingPlanet == value)
            {
                return;
            }

            _isStartingPlanet = value;
            OnPropertyChanged();
        }
    }

    public void RefreshStartIndicator(ProjectModel project)
    {
        IsStartingPlanet = string.Equals(Planet.Name, project.StartingPlanetName, StringComparison.OrdinalIgnoreCase);
    }

    protected override void RenameModel(string newName)
    {
        var oldName = Planet.Name;
        Planet.Name = newName;

        if (string.Equals(_project.StartingPlanetName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            _project.StartingPlanetName = newName;
        }

        RefreshStartIndicator(_project);
    }
}
