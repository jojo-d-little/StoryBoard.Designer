using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Scripting;

public sealed class ScriptUnknownReferenceRule : IValidationRule
{
    private static readonly System.Text.RegularExpressions.Regex UnknownReferenceTokenPattern =
        new("Unknown reference '(?:\\{)?(?<token>[^'}]+)(?:\\})?'", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "SCR-001",
        Title: "Script Unknown Reference",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Scripting");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not ValidationActionCandidateNode actionCandidate)
        {
            yield break;
        }

        var project = context.Project;
        ArgumentNullException.ThrowIfNull(project);

        var knownTokens = context.SharedState.GetOrAdd(
            "SCR:KnownReferenceTokens",
            () => ScriptRuleSupport.BuildKnownReferenceTokens(project));

        var actionEchoTokenRegistry = context.SharedState.GetOrAdd(
            "SCR:ActionEchoReferenceTokenRegistry",
            static () => ActionEchoReferenceTokenProviderRegistry.CreateDefault());

        var actionTokens = actionCandidate.Action.GetReferenceTokens();
        var actionEchoTokens = actionEchoTokenRegistry.GetTokens(actionCandidate.Action.ActionType);
        var referenceTokens = ActionOutputReferenceTokenNormalizer.ExpandIndexedActionAliases(
            ActionOutputReferenceTokenNormalizer.ExpandCurrentActionAnchors(
                    knownTokens
                .Concat(actionTokens)
                .Concat(actionEchoTokens)
                .Where(static token => !string.IsNullOrWhiteSpace(token))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var scriptField in actionCandidate.Action.GetScriptFields())
        {
            foreach (var issue in ScriptRuleSupport.AnalyzeScriptField(
                         actionCandidate.OwnerScopePath,
                         actionCandidate.Action.Name,
                         scriptField.FieldName,
                         scriptField.Value,
                         referenceTokens,
                         Metadata.RuleId,
                         Metadata.DefaultSeverity,
                         message => IsUnknownReferenceMessage(message)
                                    && !IsKnownAnchorRootedUnknownReference(message)))
            {
                yield return issue;
            }
        }
    }

    private static bool IsUnknownReferenceMessage(string message)
    {
        return message.Contains("Unknown reference", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownAnchorRootedUnknownReference(string message)
    {
        var match = UnknownReferenceTokenPattern.Match(message ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        var token = match.Groups["token"].Value.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token.StartsWith("currentAction.", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("currentAction::", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "currentAction", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return RuntimeAnchorReferenceTokenCatalog.IsKnownAnchorRootedToken(token);
    }
}
