using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SharedVariableValueRestrictionConsistencyRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-006",
        Title: "Shared Variable Restriction Consistency",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var sharedById = context.Project.SharedVariables
            .Where(static shared => shared.Id != Guid.Empty)
            .ToDictionary(shared => shared.Id);
        var linkedVariablesBySharedId = SharedVariableRuleSupport.BuildLinkedVariablesBySharedId(context.Project);

        foreach (var entry in linkedVariablesBySharedId.Where(static pair => pair.Value.Count > 1))
        {
            var participants = ResolveParticipants(entry.Value, context.Lookup);
            if (participants.Count < 2)
            {
                continue;
            }

            var distinctRestrictions = participants
                .Select(static participant => participant.ValueRestriction)
                .Distinct()
                .ToList();

            if (distinctRestrictions.Count <= 1)
            {
                continue;
            }

            var sharedLabel = sharedById.TryGetValue(entry.Key, out var shared)
                              && !string.IsNullOrWhiteSpace(shared.Name)
                ? $"{shared.Name} ({entry.Key:N})"
                : entry.Key.ToString("N");
            var participantDetails = string.Join(
                "; ",
                participants.Select(participant =>
                    $"{participant.ScopePath}:{participant.VariableName}={participant.ValueRestriction}"));

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Project",
                $"Shared variable '{sharedLabel}' has mismatched participant value restrictions. Participants: {participantDetails}.");
        }
    }

    private static List<ResolvedSharedParticipant> ResolveParticipants(
        IReadOnlyCollection<GamePropertyDefinition> variables,
        IValidationLookupService lookup)
    {
        var resolved = new List<ResolvedSharedParticipant>();

        foreach (var variable in variables)
        {
            if (variable.Id == Guid.Empty
                || !lookup.TryGetPropertyByVariableId(variable.Id, out var descriptor))
            {
                continue;
            }

            resolved.Add(new ResolvedSharedParticipant(
                variable.Id,
                variable.Name,
                variable.ValueRestriction,
                descriptor.ScopePath));
        }

        return resolved;
    }

    private sealed record ResolvedSharedParticipant(
        Guid VariableId,
        string VariableName,
        GamePropertyValueRestriction ValueRestriction,
        string ScopePath);
}