using Storyboard.Shared.GameStateData;
using Storyboard.Shared.RuntimeContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class CompositeActionMissingTargetRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-009", "Composite Action Missing Target", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        if (action.ActionType is not (CommandActionType.BuildCompositeByTarget or CommandActionType.BuildCompositeByParts))
        {
            yield break;
        }

        var targetInfo = ruleContext.SharedState.GetOrAdd("ACT:CompositeTargetLookup", () => BuildCompositeTargetLookup(ruleContext.Project));

        var payload = action.ActionType == CommandActionType.BuildCompositeByTarget
            ? ActionPayloadAccessors.GetCompositeByTargetPayload(action)
            : null;

        var payloadParts = action.ActionType == CommandActionType.BuildCompositeByParts
            ? ActionPayloadAccessors.GetCompositeByPartsPayload(action)
            : null;

        var recipeId = payload?.CompositeRecipeId ?? payloadParts?.CompositeRecipeId ?? action.CompositeRecipeId;
        var targetId = payload?.CompositeTargetObjectId ?? payloadParts?.CompositeTargetObjectId ?? action.CompositeTargetObjectId;

        if (recipeId.HasValue && recipeId.Value != Guid.Empty
            && !targetInfo.CompositeTargetByRecipeId.ContainsKey(recipeId.Value))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                $"composite action '{action.Name}' references missing composite recipe id '{recipeId.Value}'.");
            yield break;
        }

        if (targetId.HasValue && targetId.Value != Guid.Empty
            && !targetInfo.CompositeTargetObjectIds.Contains(targetId.Value))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                $"composite action '{action.Name}' references missing composite target id '{targetId.Value}'.");
        }
    }

    private static CompositeTargetLookup BuildCompositeTargetLookup(ProjectModel project)
    {
        var runtimeObjects = new List<GameObject>();

        foreach (var room in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms))
        {
            foreach (var root in room.GameObjects)
            {
                runtimeObjects.AddRange(EnumerateRuntimeObjects(root));
            }
        }

        foreach (var root in project.GlobalScope.GameObjects)
        {
            runtimeObjects.AddRange(EnumerateRuntimeObjects(root));
        }

        foreach (var scopedRoot in project.Planets
                     .SelectMany(planet => planet.GameObjects)
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.GameObjects))
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.Areas)
                         .SelectMany(area => area.GameObjects)))
        {
            runtimeObjects.AddRange(EnumerateRuntimeObjects(scopedRoot));
        }

        var compositeTargets = runtimeObjects
            .Where(static entry => entry.IsCompositeTarget)
            .ToList();

        return new CompositeTargetLookup(
            compositeTargets
                .Where(static entry => entry.CompositeRecipeId != Guid.Empty)
                .GroupBy(entry => entry.CompositeRecipeId)
                .ToDictionary(group => group.Key, group => group.First()),
            compositeTargets
                .Where(static entry => entry.ObjectId != Guid.Empty)
                .Select(static entry => entry.ObjectId)
                .ToHashSet());
    }

    private static IEnumerable<GameObject> EnumerateRuntimeObjects(GameObject rootObject)
    {
        yield return rootObject;

        foreach (var child in rootObject.ContainedObjects)
        {
            foreach (var descendant in EnumerateRuntimeObjects(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record CompositeTargetLookup(
        IReadOnlyDictionary<Guid, GameObject> CompositeTargetByRecipeId,
        IReadOnlySet<Guid> CompositeTargetObjectIds);
}
