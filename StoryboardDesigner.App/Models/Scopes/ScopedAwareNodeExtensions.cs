namespace StoryboardDesigner.App.Models;

public static class ScopedAwareNodeExtensions
{
    private static readonly string[] BuiltInDirectionalQualifiers =
    {
        "north",
        "northeast",
        "east",
        "southeast",
        "south",
        "southwest",
        "west",
        "northwest",
        "up",
        "down"
    };

    public static IEnumerable<IScopedAwareNode> EnumerateAncestors(this IScopedAwareNode? node)
    {
        var current = node?.ParentScope;
        while (current is not null)
        {
            yield return current;
            current = current.ParentScope;
        }
    }

    public static IEnumerable<IScopedAwareNode> EnumerateSelfAndAncestors(this IScopedAwareNode? node)
    {
        var current = node;
        while (current is not null)
        {
            yield return current;
            current = current.ParentScope;
        }
    }

    public static IReadOnlyList<string> GetValidVerbs(this IScopedAwareNode? node)
    {
        var values = new List<string>();

        foreach (var scopedNode in node.EnumerateSelfAndAncestors())
        {
            values.AddRange(GetAdditionalVerbs(scopedNode));
        }

        return NormalizeDistinct(values);
    }

    public static IReadOnlyList<string> GetValidDirectionalQualifiers(
        this IScopedAwareNode? node,
        bool includeBuiltIns = true)
    {
        var values = new List<string>();

        if (includeBuiltIns)
        {
            values.AddRange(BuiltInDirectionalQualifiers);
        }

        foreach (var scopedNode in node.EnumerateSelfAndAncestors())
        {
            values.AddRange(GetAdditionalDirectionals(scopedNode));
        }

        return NormalizeDistinct(values);
    }

    private static IEnumerable<string> GetAdditionalVerbs(IScopedAwareNode node)
    {
        return node switch
        {
            ProjectModel project => project.CommandVerbs,
            Planet planet => planet.AdditionalVerbs,
            Country country => country.AdditionalVerbs,
            Area area => area.AdditionalVerbs,
            Room room => room.AdditionalVerbs,
            GameObject obj => obj.AdditionalVerbs,
            _ => Array.Empty<string>()
        };
    }

    private static IEnumerable<string> GetAdditionalDirectionals(IScopedAwareNode node)
    {
        return node switch
        {
            ProjectModel project => project.Directionals,
            Planet planet => planet.AdditionalDirectionals,
            Country country => country.AdditionalDirectionals,
            Area area => area.AdditionalDirectionals,
            Room room => room.AdditionalDirectionals,
            GameObject obj => obj.AdditionalDirectionals,
            _ => Array.Empty<string>()
        };
    }

    private static IReadOnlyList<string> NormalizeDistinct(IEnumerable<string> source)
    {
        return source
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
