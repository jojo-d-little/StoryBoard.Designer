using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

/// <summary>
/// Ensures sequence IDs are usable as project-wide runtime references.
/// </summary>
public sealed class SequenceIdentityUniquenessRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "SEQ-002",
        Title: "Sequence Identity Uniqueness",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Sequences");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var definitions = EnumerateScopes(context.Project)
            .Where(static scope => scope is not GameObject { IsRoomInstance: true })
            .SelectMany(scope => (scope.SequenceDefinitions ?? new List<Storyboard.Shared.DesignerContracts.ProjectSequenceDefinitionDto>())
                .Select(definition => new SequenceIdentityEntry(scope, definition)))
            .ToList();

        foreach (var entry in definitions.Where(static entry => entry.Definition.SequenceId == Guid.Empty))
        {
            var name = DisplayName(entry.Definition);
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                BuildSequencePath(entry.Scope, entry.Definition),
                $"sequence '{name}' has an empty sequence ID.",
                "Assign a new unique sequence ID and update any actions that start or cancel this sequence.");
        }

        foreach (var group in definitions
                     .Where(static entry => entry.Definition.SequenceId != Guid.Empty)
                     .GroupBy(static entry => entry.Definition.SequenceId)
                     .Where(static group => group.Count() > 1))
        {
            foreach (var entry in group)
            {
                var otherLocations = group
                    .Where(other => !ReferenceEquals(other, entry))
                    .Select(other => BuildSequencePath(other.Scope, other.Definition));
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    BuildSequencePath(entry.Scope, entry.Definition),
                    $"sequence '{DisplayName(entry.Definition)}' uses ID '{group.Key:D}', which is also used by {string.Join("; ", otherLocations)}.",
                    "Give each sequence a unique ID and update Start Sequence or Cancel Sequence actions that should target the changed definition.");
            }
        }
    }

    private static IEnumerable<ScopeNodeBase> EnumerateScopes(IScopedAwareNode root)
    {
        var pending = new Stack<IScopedAwareNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is ScopeNodeBase scope)
            {
                yield return scope;
            }

            foreach (var child in current.ChildScopes.Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static string BuildSequencePath(ScopeNodeBase scope, Storyboard.Shared.DesignerContracts.ProjectSequenceDefinitionDto definition)
    {
        var scopePath = string.Join(" / ", scope
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
        return $"{scopePath} / Sequence[{definition.SequenceId:N}] '{DisplayName(definition)}'";
    }

    private static string DisplayName(Storyboard.Shared.DesignerContracts.ProjectSequenceDefinitionDto definition) =>
        string.IsNullOrWhiteSpace(definition.DisplayName) ? "(unnamed sequence)" : definition.DisplayName.Trim();

    private sealed record SequenceIdentityEntry(
        ScopeNodeBase Scope,
        Storyboard.Shared.DesignerContracts.ProjectSequenceDefinitionDto Definition);
}
