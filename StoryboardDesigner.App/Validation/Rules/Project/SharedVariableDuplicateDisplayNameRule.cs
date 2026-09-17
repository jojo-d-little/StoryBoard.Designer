using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SharedVariableDuplicateDisplayNameRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-009",
        Title: "Shared Variable Duplicate Display Name",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var duplicateGroups = context.Project.SharedVariables
            .Where(shared => shared.Id != Guid.Empty && !string.IsNullOrWhiteSpace(shared.Name))
            .GroupBy(shared => shared.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in duplicateGroups)
        {
            var ids = group
                .OrderBy(shared => shared.Id)
                .Select(shared => shared.Id.ToString("N"))
                .ToList();

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Project",
                $"Shared variable display name '{group.Key}' is duplicated across ids: {string.Join(", ", ids)}.",
                "IDs remain canonical identity; consider renaming for readability.");
        }
    }
}
