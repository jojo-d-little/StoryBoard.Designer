using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using Storyboard.Shared.RuntimeContracts.Dtos;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SequenceDefinitionIntegrityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        Enum.GetValues<ScopeNodeKind>().ToHashSet();

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "SEQ-001",
        Title: "Authored Sequence Integrity",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Sequences");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is GameObject { IsRoomInstance: true })
        {
            yield break;
        }

        var scopePath = EventSubscriptionRuleSupport.BuildScopePath(context.CandidateNode);
        foreach (var sequence in context.CandidateNode.SequenceDefinitions ?? new List<ProjectSequenceDefinitionDto>())
        {
            var sequenceName = string.IsNullOrWhiteSpace(sequence.DisplayName) ? "(unnamed sequence)" : sequence.DisplayName.Trim();
            var sequencePath = $"{scopePath} / Sequence[{sequence.SequenceId:N}] '{sequenceName}'";
            foreach (var issue in SequenceDefinitionIntegrityEvaluator.Evaluate(sequence))
            {
                var issuePath = issue.StepId.HasValue
                    ? $"{sequencePath} / Step {issue.StepId.Value:N}"
                    : sequencePath;
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    issuePath,
                    issue.Description,
                    issue.Hint);
            }
        }
    }
}
