namespace StoryboardDesigner.App.ViewModels;

public sealed class AreaSettingsNodeViewModel : HierarchyNodeViewModel
{
    public AreaSettingsNodeViewModel(AreaNodeViewModel areaNode)
        : base("Area Settings", areaNode)
    {
        AreaNode = areaNode;
        NodeTypeLabel = "Area Settings";
    }

    public AreaNodeViewModel AreaNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
