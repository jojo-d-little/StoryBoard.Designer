namespace StoryboardDesigner.App.Services;

internal static class ActionOutputReferenceTokenNormalizer
{
    public static IReadOnlyList<string> ExpandCurrentActionAnchors(IEnumerable<string>? tokens)
    {
        var expanded = new List<string>();

        foreach (var token in tokens ?? Array.Empty<string>())
        {
            var normalized = token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            expanded.Add(normalized);
        }

        return expanded
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ExpandIndexedActionAliases(IEnumerable<string>? tokens)
    {
        var expanded = new List<string>();

        foreach (var token in tokens ?? Array.Empty<string>())
        {
            var normalized = token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            expanded.Add(normalized);

            if (normalized.StartsWith("currentAction.", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add($"{normalized}[0]");
            }
        }

        return expanded
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
