using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Execution;

internal static class ValidationOwnedCatalogTraversal
{
    public static IEnumerable<(string CatalogName, ScopeNodeBase Entry)> Enumerate(ScopeNodeBase owner)
    {
        switch (owner)
        {
            case Planet planet:
                foreach (var entry in Enumerate(planet.BaseObjects, planet.ObjectTemplates, planet.RoomTemplates))
                    yield return entry;
                break;
            case Country country:
                foreach (var entry in Enumerate(country.BaseObjects, country.ObjectTemplates, country.RoomTemplates))
                    yield return entry;
                break;
            case Area area:
                foreach (var entry in Enumerate(area.BaseObjects, area.ObjectTemplates, area.RoomTemplates))
                    yield return entry;
                break;
        }
    }

    private static IEnumerable<(string CatalogName, ScopeNodeBase Entry)> Enumerate(
        IEnumerable<GameObject> baseObjects,
        IEnumerable<GameObject> objectTemplates,
        IEnumerable<Room> roomTemplates)
    {
        foreach (var obj in baseObjects)
            yield return ("Base Objects", obj);

        foreach (var obj in objectTemplates)
            yield return ("Object Templates", obj);

        foreach (var room in roomTemplates)
            yield return ("Room Templates", room);
    }
}
