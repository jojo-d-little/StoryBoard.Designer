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

        switch (parent)
        {
            case Planet planet:
                AttachScopedCatalogs(planet, planet.BaseObjects, planet.ObjectTemplates, planet.RoomTemplates);
                break;
            case Country country:
                AttachScopedCatalogs(country, country.BaseObjects, country.ObjectTemplates, country.RoomTemplates);
                break;
            case Area area:
                AttachScopedCatalogs(area, area.BaseObjects, area.ObjectTemplates, area.RoomTemplates);
                break;
        }
    }

    private static void AttachScopedCatalogs(
        ScopeNodeBase owner,
        IEnumerable<GameObject> baseObjects,
        IEnumerable<GameObject> objectTemplates,
        IEnumerable<Room> roomTemplates)
    {
        foreach (var baseObject in baseObjects)
        {
            baseObject.ParentScope = owner;
            AttachChildren(baseObject);
        }

        foreach (var template in objectTemplates)
        {
            template.ParentScope = owner;
            AttachChildren(template);
        }

        foreach (var template in roomTemplates)
        {
            template.ParentScope = owner;
            AttachChildren(template);
        }
    }
}
