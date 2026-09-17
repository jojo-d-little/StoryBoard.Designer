namespace StoryboardDesigner.App.ViewModels;

public sealed class PlanetSettingsNodeViewModel : HierarchyNodeViewModel
{
    public PlanetSettingsNodeViewModel(PlanetNodeViewModel planetNode)
        : base("Planet Settings", planetNode)
    {
        PlanetNode = planetNode;
        NodeTypeLabel = "Planet Settings";
    }

    public PlanetNodeViewModel PlanetNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
