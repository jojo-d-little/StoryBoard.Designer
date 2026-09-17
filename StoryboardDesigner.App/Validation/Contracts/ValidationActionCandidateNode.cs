using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Contracts;

public sealed class ValidationActionCandidateNode : ScopeNodeBase
{
    public ValidationActionCandidateNode(CommandAction action, ScopeNodeBase ownerScope, string ownerScopePath)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        OwnerScope = ownerScope ?? throw new ArgumentNullException(nameof(ownerScope));
        OwnerScopePath = ownerScopePath ?? string.Empty;
        ParentScope = ownerScope;
    }

    public CommandAction Action { get; }
    public ScopeNodeBase OwnerScope { get; }
    public string OwnerScopePath { get; }

    // ScopeNodeKind has no Action value; action candidate routing is handled
    // explicitly by ValidationEngine using SupportsActionCandidates.
    public override ScopeNodeKind ScopeKind => ScopeNodeKind.GameObject;
    public override string ScopeName => string.IsNullOrWhiteSpace(Action.Name) ? "(unnamed action)" : Action.Name;
    public override IEnumerable<string> ScopeTokens => Array.Empty<string>();
    public override IEnumerable<IScopedAwareNode> ChildScopes => Array.Empty<IScopedAwareNode>();

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        return false;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return false;
    }
}