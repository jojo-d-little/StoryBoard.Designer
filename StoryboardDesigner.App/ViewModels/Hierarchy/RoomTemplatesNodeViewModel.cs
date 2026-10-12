using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomTemplatesNodeViewModel : HierarchyNodeViewModel
{
    public RoomTemplatesNodeViewModel(ProjectModel project, HierarchyNodeViewModel parent)
        : this(
            project,
            parent,
            "Room Templates",
            project.RoomTemplates,
            project.RoomTemplatesIgnoredValidationRuleIds,
            new RoomTemplatesScopeNode(project))
    {
    }

    public RoomTemplatesNodeViewModel(
        ProjectModel project,
        HierarchyNodeViewModel parent,
        string displayName,
        List<Room> roomTemplates,
        List<string> catalogIgnoredValidationRuleIds,
        ScopeNodeBase catalogParentScope)
        : base(displayName, parent)
    {
        Project = project;
        RoomTemplates = roomTemplates;
        CatalogIgnoredValidationRuleIds = catalogIgnoredValidationRuleIds;
        CatalogParentScope = catalogParentScope;
        ScopeOwner = catalogParentScope;
        NodeTypeLabel = displayName;
    }

    public ProjectModel Project { get; }
    public List<Room> RoomTemplates { get; }
    public List<string> CatalogIgnoredValidationRuleIds { get; }
    public ScopeNodeBase CatalogParentScope { get; }

    protected override void RenameModel(string newName)
    {
    }
}
