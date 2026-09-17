using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SharedVariableOrphanMetadataShellRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-008",
        Title: "Shared Variable Orphan Metadata Shell",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var linkedVariablesBySharedId = SharedVariableRuleSupport.BuildLinkedVariablesBySharedId(context.Project);
        foreach (var shared in context.Project.SharedVariables.Where(static shared => shared.Id != Guid.Empty))
        {
            if (linkedVariablesBySharedId.TryGetValue(shared.Id, out var linked) && linked.Count > 0)
            {
                continue;
            }

            var sharedLabel = string.IsNullOrWhiteSpace(shared.Name)
                ? shared.Id.ToString("N")
                : $"{shared.Name} ({shared.Id:N})";

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Project",
                $"Shared variable metadata shell '{sharedLabel}' has no linked participant variables.",
                "Keep as placeholder or delete it explicitly in shared-variable management UX.");
        }
    }
}
