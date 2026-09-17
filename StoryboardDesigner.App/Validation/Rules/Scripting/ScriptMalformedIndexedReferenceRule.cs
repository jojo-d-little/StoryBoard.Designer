using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Scripting;

public sealed class ScriptMalformedIndexedReferenceRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "SCR-002",
        Title: "Script Malformed Indexed Reference",
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

        var actionTokens = actionCandidate.Action.GetReferenceTokens();
        var referenceTokens = knownTokens
            .Concat(actionTokens)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
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
                         static message => message.Contains("Malformed indexed reference", StringComparison.OrdinalIgnoreCase)))
            {
                yield return issue;
            }
        }
    }
}
