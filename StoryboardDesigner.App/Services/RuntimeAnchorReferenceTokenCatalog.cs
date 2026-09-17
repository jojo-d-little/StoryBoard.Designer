using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

internal static class RuntimeAnchorReferenceTokenCatalog
{
    private static readonly Lazy<IReadOnlyList<string>> KnownAnchorKeys = new(LoadKnownAnchorKeys);

    public static IReadOnlyList<string> GetKnownAnchorKeys()
    {
        return KnownAnchorKeys.Value;
    }

    public static IReadOnlyList<string> BuildIntrinsicAnchorTokens()
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var anchorKey in GetKnownAnchorKeys())
        {
            tokens.Add(anchorKey);

            tokens.Add($"{anchorKey}.name");
            tokens.Add($"{anchorKey}.nameInGame");
        }

        return tokens
            .OrderBy(static token => token, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsKnownAnchorRootedToken(string? token)
    {
        var normalized = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var rootAnchor = ExtractRootAnchor(normalized);
        return !string.IsNullOrWhiteSpace(rootAnchor)
            && GetKnownAnchorKeys().Contains(rootAnchor, StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractRootAnchor(string token)
    {
        var separatorIndex = token.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            return token[..separatorIndex].Trim();
        }

        var dotIndex = token.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex > 0)
        {
            return token[..dotIndex].Trim();
        }

        return token.Trim();
    }

    private static IReadOnlyList<string> LoadKnownAnchorKeys()
    {
        var anchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var anchorKey in new RuntimeSessionAnchorManifestReader().Load().GetOrderedAnchorKeys())
            {
                if (!string.IsNullOrWhiteSpace(anchorKey))
                {
                    anchors.Add(anchorKey.Trim());
                }
            }
        }
        catch
        {
        }

        anchors.Add("self");
        anchors.Add("currentAction");
        anchors.Add("currentPhase");
        anchors.Add("currentRoom");
        anchors.Add("priorRoom");

        return anchors
            .OrderBy(static anchor => anchor, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
