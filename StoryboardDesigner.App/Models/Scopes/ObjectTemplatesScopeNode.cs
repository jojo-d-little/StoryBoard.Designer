namespace StoryboardDesigner.App.Models;

public sealed class ObjectTemplatesScopeNode : ScopeNodeBase
{
    private readonly ProjectModel _project;

    public ObjectTemplatesScopeNode(ProjectModel project)
    {
        _project = project;
    }

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Templates;
    public override string ScopeName => "Object Templates";
    public override IEnumerable<string> ScopeTokens => new[] { "templates" };
    public override List<string> IgnoredValidationRuleIds
    {
        get => _project.ObjectTemplatesIgnoredValidationRuleIds;
        set => _project.ObjectTemplatesIgnoredValidationRuleIds = value ?? new List<string>();
    }
    public override IEnumerable<IScopedAwareNode> ChildScopes => _project.ObjectTemplates;

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

        if (_project.ObjectTemplates.Contains(obj))
        {
            return true;
        }

        _project.ObjectTemplates.Add(obj);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return child is GameObject obj && _project.ObjectTemplates.Remove(obj);
    }
}
