namespace StoryboardDesigner.App.Services;

public static class EchoQuickReferenceTokenFilter
{
    public static IReadOnlyList<string> FilterQuickBraceTokens(IEnumerable<string>? tokens)
    {
        return (tokens ?? Array.Empty<string>())
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Select(static token => token.Trim())
            .Where(IsQuickBraceToken)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetQuickBracePriority)
            .ThenBy(static token => token, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsQuickBraceToken(string token)
    {
        return token.StartsWith("currentAction.", StringComparison.OrdinalIgnoreCase)
               || token.StartsWith("self.", StringComparison.OrdinalIgnoreCase)
               || RuntimeAnchorReferenceTokenCatalog.IsKnownAnchorRootedToken(token);
    }

    private static int GetQuickBracePriority(string token)
    {
        if (token.StartsWith("currentAction.", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (token.StartsWith("self.", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (RuntimeAnchorReferenceTokenCatalog.IsKnownAnchorRootedToken(token))
        {
            return 2;
        }

        return 3;
    }
}
