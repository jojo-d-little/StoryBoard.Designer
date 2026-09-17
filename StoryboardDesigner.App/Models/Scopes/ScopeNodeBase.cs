namespace StoryboardDesigner.App.Models;

public abstract class ScopeNodeBase : IScopedAwareNode
{
    public abstract ScopeNodeKind ScopeKind { get; }
    public virtual ScopeNodeKind? DesignerPersistenceScopeKind { get; set; }
    public abstract string ScopeName { get; }
    public virtual string ScopeNameInGame => ScopeName;
    public abstract IEnumerable<string> ScopeTokens { get; }
    public IEnumerable<string> ValidationErrors { get; set; } = Array.Empty<string>();
    public virtual List<string> IgnoredValidationRuleIds { get; set; } = new();
    public virtual List<EventSubscriptionDefinition> EventSubscriptions { get; set; } = new();
    public virtual List<Storyboard.Shared.RuntimeContracts.Dtos.RuntimeTimerDefinitionDto> TimerDefinitions { get; set; } = new();
    public IScopedAwareNode? ParentScope { get; set; }
    public abstract IEnumerable<IScopedAwareNode> ChildScopes { get; }

    public virtual bool AddChildScope(IScopedAwareNode child)
    {
        if (child is null || ReferenceEquals(child, this))
        {
            return false;
        }

        if (!CanAcceptChild(child))
        {
            return false;
        }

        if (ReferenceEquals(child.ParentScope, this))
        {
            return true;
        }

        child.ParentScope?.RemoveChildScope(child);
        if (!TryAddChildCore(child))
        {
            return false;
        }

        child.ParentScope = this;
        return true;
    }

    public virtual bool RemoveChildScope(IScopedAwareNode child)
    {
        if (child is null)
        {
            return false;
        }

        var removed = TryRemoveChildCore(child);
        if (removed && ReferenceEquals(child.ParentScope, this))
        {
            child.ParentScope = null;
        }

        return removed;
    }

    protected virtual bool CanAcceptChild(IScopedAwareNode child)
    {
        return true;
    }

    protected abstract bool TryAddChildCore(IScopedAwareNode child);
    protected abstract bool TryRemoveChildCore(IScopedAwareNode child);
}
