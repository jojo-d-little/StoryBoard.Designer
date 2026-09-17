using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Execution;

internal sealed class ScopeAttachedBaseObjectsNode : ScopeNodeBase
{
    private readonly List<GameObject> _objects;
    private readonly List<string> _ignoredValidationRuleIds;

    public ScopeAttachedBaseObjectsNode(List<GameObject> objects, List<string> ignoredValidationRuleIds, ScopeNodeBase parentScope)
    {
        _objects = objects;
        _ignoredValidationRuleIds = ignoredValidationRuleIds;
        ParentScope = parentScope;
    }

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Templates;
    public override string ScopeName => "Base Objects";
    public override IEnumerable<string> ScopeTokens => new[] { "base", "baseobjects" };
    public override List<string> IgnoredValidationRuleIds
    {
        get => _ignoredValidationRuleIds;
        set
        {
            _ignoredValidationRuleIds.Clear();
            if (value is null)
            {
                return;
            }

            _ignoredValidationRuleIds.AddRange(value);
        }
    }

    public override IEnumerable<IScopedAwareNode> ChildScopes => _objects;

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        if (child is not GameObject obj)
        {
            return false;
        }

        if (_objects.Contains(obj))
        {
            return true;
        }

        _objects.Add(obj);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return child is GameObject obj && _objects.Remove(obj);
    }
}
