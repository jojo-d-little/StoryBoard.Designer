namespace StoryboardDesigner.App.Services;

public static class VariableReferenceSyntax
{
    public const string AnchorRootDelimiter = "::";

    public static string BuildAnchoredReference(string anchorKey, string referencePath)
    {
        var safeAnchor = anchorKey?.Trim() ?? string.Empty;
        var safePath = referencePath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(safeAnchor))
        {
            return safePath;
        }

        return string.IsNullOrWhiteSpace(safePath)
            ? safeAnchor
            : $"{safeAnchor}{AnchorRootDelimiter}{safePath}";
    }

    public static string BuildAnchoredReference(string anchorKey, string subPropertyKey, string variableName)
    {
        var safeSubProperty = subPropertyKey?.Trim() ?? string.Empty;
        var safeVariable = variableName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(safeSubProperty))
        {
            return BuildAnchoredReference(anchorKey, safeVariable);
        }

        var path = string.IsNullOrWhiteSpace(safeVariable)
            ? safeSubProperty
            : $"{safeSubProperty}.{safeVariable}";

        return BuildAnchoredReference(anchorKey, path);
    }

    public static bool TryParseAnchoredReference(string reference, out string anchorKey, out string referencePath)
    {
        anchorKey = string.Empty;
        referencePath = string.Empty;

        var safeReference = reference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(safeReference))
        {
            return false;
        }

        var delimiterIndex = safeReference.IndexOf(AnchorRootDelimiter, StringComparison.Ordinal);
        if (delimiterIndex <= 0)
        {
            return false;
        }

        var anchor = safeReference[..delimiterIndex].Trim();
        var pathStart = delimiterIndex + AnchorRootDelimiter.Length;
        var path = pathStart < safeReference.Length
            ? safeReference[pathStart..].Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(anchor) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        anchorKey = anchor;
        referencePath = path;
        return true;
    }
}
