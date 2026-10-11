using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

/// <summary>
/// Ensures sequence actions still name an existing authored sequence. The current
/// sequence picker intentionally offers definitions across the project, so this rule
/// validates identity without imposing a narrower room-scope policy.
/// </summary>
public sealed class SequenceActionTargetExistsRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-020",
        "Sequence Action Target Exists",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        var sequenceId = action.ActionType switch
        {
            CommandActionType.StartSequence => ActionPayloadAccessors.GetStartSequenceId(action),
            CommandActionType.CancelSequence => ActionPayloadAccessors.GetCancelSequenceId(action),
            _ => Guid.Empty
        };

        if (action.ActionType is not (CommandActionType.StartSequence or CommandActionType.CancelSequence))
        {
            yield break;
        }

        var sequences = ruleContext.SharedState.GetOrAdd(
            "ACT:ProjectSequenceIds",
            () => BuildSequenceLookup(ruleContext.Project));

        if (sequenceId != Guid.Empty && sequences.ContainsKey(sequenceId))
        {
            yield break;
        }

        var actionName = string.IsNullOrWhiteSpace(action.Name) ? "(unnamed action)" : action.Name.Trim();
        var message = sequenceId == Guid.Empty
            ? $"{action.ActionType} action '{actionName}' has no sequence selected."
            : $"{action.ActionType} action '{actionName}' references sequence id '{sequenceId:D}', which is not defined in the project.";

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            message,
            "Choose an existing sequence definition in the action editor.");
    }

    private static Dictionary<Guid, ProjectSequenceDefinitionDto> BuildSequenceLookup(ProjectModel project)
    {
        var result = new Dictionary<Guid, ProjectSequenceDefinitionDto>();
        var visited = new HashSet<IScopedAwareNode>(ReferenceEqualityComparer.Instance);

        void Visit(IScopedAwareNode node)
        {
            if (!visited.Add(node))
            {
                return;
            }

            if (node is ScopeNodeBase scope)
            {
                if (scope is GameObject { IsRoomInstance: true })
                {
                    foreach (var child in node.ChildScopes)
                    {
                        Visit(child);
                    }

                    return;
                }

                foreach (var definition in scope.SequenceDefinitions ?? new List<ProjectSequenceDefinitionDto>())
                {
                    if (definition.SequenceId != Guid.Empty)
                    {
                        result.TryAdd(definition.SequenceId, definition);
                    }
                }
            }

            foreach (var child in node.ChildScopes)
            {
                Visit(child);
            }
        }

        Visit(project);

        // Base object catalogs attached to planets, countries, and areas are not
        // part of those scopes' ChildScopes collections. Include their definitions
        // explicitly so actions can target sequences owned by lower-scope bases.
        var baseObjectRoots = project.BaseObjects
            .Concat(project.Planets.SelectMany(static planet => planet.BaseObjects))
            .Concat(project.Planets
                .SelectMany(static planet => planet.Countries)
                .SelectMany(static country => country.BaseObjects))
            .Concat(project.Planets
                .SelectMany(static planet => planet.Countries)
                .SelectMany(static country => country.Areas)
                .SelectMany(static area => area.BaseObjects));

        foreach (var baseObject in baseObjectRoots)
        {
            Visit(baseObject);
        }

        return result;
    }
}
