using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class ProcedureRequiresMutationRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-011",
        Title: "Procedure Requires Mutation",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        foreach (var procedure in context.Project.Procedures)
        {
            if (procedure.ParticipantMutations.Count > 0)
            {
                continue;
            }

            var procedureLabel = NormalizeName(procedure.Name, "Procedure");
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                $"Project / Procedures / {procedureLabel}",
                $"Procedure '{procedureLabel}' has no participant mutations defined.",
                "Add at least one mutation in Procedure Designer or remove the unused procedure.");
        }
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}