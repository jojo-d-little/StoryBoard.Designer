namespace StoryboardDesigner.App.Models;

public interface IScopedAwareNode
{
    ScopeNodeKind ScopeKind { get; }
    string ScopeName { get; }
    string ScopeNameInGame { get; }
    IEnumerable<string> ScopeTokens { get; }
    IScopedAwareNode? ParentScope { get; set; }
    IEnumerable<IScopedAwareNode> ChildScopes { get; }
    bool AddChildScope(IScopedAwareNode child);
    bool RemoveChildScope(IScopedAwareNode child);
}
