using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class PhaseBooksNodeViewModel : HierarchyNodeViewModel
{
    public PhaseBooksNodeViewModel(ProjectModel project, HierarchyNodeViewModel parent)
        : base("Phases", parent)
    {
        Project = project;
        NodeTypeLabel = "Phases";
    }

    public ProjectModel Project { get; }

    protected override void RenameModel(string newName)
    {
    }
}
