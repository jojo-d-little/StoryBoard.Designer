namespace StoryboardDesigner.App.Validation.Contracts;

public static class EmptyScopeNodeKindSet
{
    public static readonly IReadOnlySet<ScopeNodeKind> Instance = new HashSet<ScopeNodeKind>();
}
