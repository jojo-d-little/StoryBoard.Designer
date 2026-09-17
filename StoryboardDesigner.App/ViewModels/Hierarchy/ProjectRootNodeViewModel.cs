using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ProjectRootNodeViewModel : HierarchyNodeViewModel
{
    public ProjectRootNodeViewModel(ProjectModel project)
        : base("Global")
    {
        Project = project;
        NodeTypeLabel = "Global";
    }

    public ProjectModel Project { get; }

    protected override void RenameModel(string newName)
    {
    }
}
