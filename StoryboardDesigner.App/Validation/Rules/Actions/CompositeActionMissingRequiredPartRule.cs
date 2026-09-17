using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class CompositeActionMissingRequiredPartRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-010", "Composite Action Missing Required Part", ValidationSeverity.Warning, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        var requiredPartIds = GetRequiredPartIds(action);
        if (requiredPartIds.Count == 0)
        {
            yield break;
        }

        var knownObjectIds = ruleContext.SharedState.GetOrAdd(
            "ACT:KnownProjectObjectIds",
            () => BuildKnownObjectIdSet(ruleContext.Project));

        var missingPartIds = requiredPartIds
            .Where(id => id != Guid.Empty && !knownObjectIds.Contains(id))
            .Distinct()
            .ToList();

        if (missingPartIds.Count == 0)
        {
            yield break;
        }

        var missingList = string.Join(", ", missingPartIds.Select(id => id.ToString("N")));
        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            $"composite action '{action.Name}' references required part id(s) that do not exist in the project: {missingList}.");
    }

    private static IReadOnlyList<Guid> GetRequiredPartIds(CommandAction action)
    {
        return action.ActionType switch
        {
            CommandActionType.BuildCompositeByTarget => ActionPayloadAccessors.GetCompositeByTargetPayload(action).CompositeRequiredPartObjectIds,
            CommandActionType.BuildCompositeByParts => ActionPayloadAccessors.GetCompositeByPartsPayload(action).CompositeRequiredPartObjectIds,
            CommandActionType.BreakCompositeItem => ActionPayloadAccessors.GetBreakCompositePayload(action).CompositeRequiredPartObjectIds,
            _ => Array.Empty<Guid>()
        };
    }

    private static HashSet<Guid> BuildKnownObjectIdSet(ProjectModel project)
    {
        var known = new HashSet<Guid>();

        static void AddObject(GameObject obj, ISet<Guid> set)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                set.Add(obj.ObjectId);
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddObject(child, set);
            }
        }

        foreach (var template in project.ObjectTemplates)
        {
            AddObject(template, known);
        }

        foreach (var baseObject in project.BaseObjects)
        {
            AddObject(baseObject, known);
        }

        foreach (var playerObject in project.GlobalScope.GameObjects)
        {
            AddObject(playerObject, known);
        }

        foreach (var scopedObject in project.Planets
                     .SelectMany(planet => planet.GameObjects)
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.GameObjects))
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.Areas)
                         .SelectMany(area => area.GameObjects)))
        {
            AddObject(scopedObject, known);
        }

        foreach (var roomObject in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms)
                     .SelectMany(room => room.GameObjects))
        {
            AddObject(roomObject, known);
        }

        foreach (var scopedBaseObject in project.Planets
                     .SelectMany(planet => planet.BaseObjects)
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.BaseObjects))
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.Areas)
                         .SelectMany(area => area.BaseObjects)))
        {
            AddObject(scopedBaseObject, known);
        }

        return known;
    }
}