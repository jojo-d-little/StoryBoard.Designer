using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomTemplatesNodeViewModel : HierarchyNodeViewModel
{
    public RoomTemplatesNodeViewModel(ProjectModel project, HierarchyNodeViewModel parent)
        : base("Room Templates", parent)
    {
        Project = project;
        RoomTemplates = project.RoomTemplates;
        CatalogIgnoredValidationRuleIds = project.RoomTemplatesIgnoredValidationRuleIds;
        CatalogParentScope = new RoomTemplatesScopeNode(project);
        NodeTypeLabel = "Room Templates";
    }

    public ProjectModel Project { get; }
    public List<Room> RoomTemplates { get; }
    public List<string> CatalogIgnoredValidationRuleIds { get; }
    public ScopeNodeBase CatalogParentScope { get; }

    protected override void RenameModel(string newName)
    {
    }
}
