using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ObjectTemplatesNodeViewModel : HierarchyNodeViewModel
{
    public ObjectTemplatesNodeViewModel(ProjectModel project, HierarchyNodeViewModel parent, bool isBaseCatalog = false)
        : this(
            project,
            parent,
            isBaseCatalog ? "Base Objects" : "Object Templates",
            isBaseCatalog ? project.BaseObjects : project.ObjectTemplates,
            isBaseCatalog ? project.BaseObjectsIgnoredValidationRuleIds : project.ObjectTemplatesIgnoredValidationRuleIds,
            isBaseCatalog,
            isBaseCatalog ? new BaseObjectsScopeNode(project) : new ObjectTemplatesScopeNode(project))
    {
    }

    public ObjectTemplatesNodeViewModel(
        ProjectModel project,
        HierarchyNodeViewModel parent,
        string displayName,
        List<GameObject> catalogObjects,
        List<string> catalogIgnoredValidationRuleIds,
        bool isBaseCatalog,
        ScopeNodeBase? catalogParentScope)
        : base(displayName, parent)
    {
        Project = project;
        CatalogObjects = catalogObjects;
        CatalogIgnoredValidationRuleIds = catalogIgnoredValidationRuleIds;
        IsBaseCatalog = isBaseCatalog;
        CatalogParentScope = catalogParentScope;
        NodeTypeLabel = displayName;
    }

    public ProjectModel Project { get; }
    public bool IsBaseCatalog { get; }
    public List<GameObject> CatalogObjects { get; }
    public List<string> CatalogIgnoredValidationRuleIds { get; }
    public ScopeNodeBase? CatalogParentScope { get; }

    protected override void RenameModel(string newName)
    {
    }
}
