using System.Text.RegularExpressions;
using Storyboard.Shared.GameServices.Actions;

namespace StoryboardDesigner.App.Services;

public static class ActionScriptEditorDiagnosticsAnalyzer
{
    private static readonly Regex BraceReferencePattern = new("\\{([^{}]+)\\}", RegexOptions.Compiled);
    private static readonly Regex IndexedTokenPattern = new("^[A-Za-z_][A-Za-z0-9_.]*(?:\\[(?:0|[1-9]\\d*)\\])?$", RegexOptions.Compiled);

    public static ActionScriptEditorDiagnosticsResult Analyze(string scriptText, IEnumerable<string>? referenceTokens)
    {
        var result = new ActionScriptEditorDiagnosticsResult();
        var validation = ActionScriptSyntaxValidator.Validate(scriptText ?? string.Empty, referenceTokens);

        foreach (var error in validation.Errors)
        {
            if (error.Contains("Unknown reference", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add(error);
                continue;
            }

            result.Errors.Add(error);
        }

        foreach (var warning in FindMalformedIndexedReferenceWarnings(scriptText ?? string.Empty))
        {
            result.Warnings.Add(warning);
        }

        Deduplicate(result.Errors);
        Deduplicate(result.Warnings);

        return result;
    }

    private static IEnumerable<string> FindMalformedIndexedReferenceWarnings(string scriptText)
    {
        foreach (Match match in BraceReferencePattern.Matches(scriptText))
        {
            var token = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!token.Contains('[', StringComparison.Ordinal) && !token.Contains(']', StringComparison.Ordinal))
            {
                continue;
            }

            if (IndexedTokenPattern.IsMatch(token))
            {
                continue;
            }

            yield return $"Malformed indexed reference '{{{token}}}' detected. Use '<token>[n]' with a non-negative integer index.";
        }
    }

    private static void Deduplicate(List<string> messages)
    {
        if (messages.Count <= 1)
        {
            return;
        }

        var deduped = messages
            .Where(static message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        messages.Clear();
        messages.AddRange(deduped);
    }
}
