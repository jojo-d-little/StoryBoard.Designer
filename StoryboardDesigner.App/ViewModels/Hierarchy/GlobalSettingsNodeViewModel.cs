namespace StoryboardDesigner.App.ViewModels;

public sealed class GlobalSettingsNodeViewModel : HierarchyNodeViewModel
{
    public GlobalSettingsNodeViewModel(ProjectRootNodeViewModel projectNode)
        : base("Global Settings", projectNode)
    {
        ProjectNode = projectNode;
        NodeTypeLabel = "Global Settings";
    }

    public ProjectRootNodeViewModel ProjectNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
