namespace StoryboardDesigner.App.Models;

public sealed class BaseObjectsScopeNode : ScopeNodeBase
{
    private readonly ProjectModel _project;

    public BaseObjectsScopeNode(ProjectModel project)
    {
        _project = project;
    }

    // Keep runtime scope mapping compatible for now; Base Objects are a designer-only catalog surface.
    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Templates;
    public override string ScopeName => "Base Objects";
    public override IEnumerable<string> ScopeTokens => new[] { "base", "baseobjects" };
    public override List<string> IgnoredValidationRuleIds
    {
        get => _project.BaseObjectsIgnoredValidationRuleIds;
        set => _project.BaseObjectsIgnoredValidationRuleIds = value ?? new List<string>();
    }
    public override IEnumerable<IScopedAwareNode> ChildScopes => _project.BaseObjects;

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child.ScopeKind == ScopeNodeKind.GameObject;
    }

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        if (child is not GameObject obj)
        {
            return false;
        }

        if (_project.BaseObjects.Contains(obj))
        {
            return true;
        }

        _project.BaseObjects.Add(obj);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return child is GameObject obj && _project.BaseObjects.Remove(obj);
    }
}
