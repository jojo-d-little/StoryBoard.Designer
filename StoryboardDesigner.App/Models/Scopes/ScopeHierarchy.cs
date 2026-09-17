namespace StoryboardDesigner.App.Models;

public static class ScopeHierarchy
{
    public static void AttachParents(ProjectModel? project)
    {
        if (project is null)
        {
            return;
        }

        AttachChildren(project);
    }

    private static void AttachChildren(IScopedAwareNode parent)
    {
        var children = (parent.ChildScopes ?? Array.Empty<IScopedAwareNode>()).ToList();
        foreach (var child in children)
        {
            parent.AddChildScope(child);
            AttachChildren(child);
        }
    }
}
