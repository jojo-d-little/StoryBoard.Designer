using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class BuildCompositeByPartsTargetRecipeMismatchRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-012", "BuildCompositeByParts Target Recipe Mismatch", ValidationSeverity.Warning, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        if (action.ActionType != CommandActionType.BuildCompositeByParts)
        {
            yield break;
        }

        var payload = ActionPayloadAccessors.GetCompositeByPartsPayload(action);
        if (!payload.CompositeTargetObjectId.HasValue
            || payload.CompositeTargetObjectId.Value == Guid.Empty
            || !payload.CompositeRecipeId.HasValue
            || payload.CompositeRecipeId.Value == Guid.Empty)
        {
            yield break;
        }

        var objectLookup = ruleContext.SharedState.GetOrAdd(
            "ACT:KnownProjectObjectLookup",
            () => BuildObjectLookup(ruleContext.Project));

        if (!objectLookup.TryGetValue(payload.CompositeTargetObjectId.Value, out var targetObject))
        {
            // ACT-011 handles missing target object ID presence checks.
            yield break;
        }

        if (targetObject.CompositeRecipeId == payload.CompositeRecipeId.Value)
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            $"BuildCompositeByParts action '{action.Name}' references recipe id '{payload.CompositeRecipeId.Value:N}', but target object '{targetObject.Name}' does not use that recipe id.");
    }

    private static Dictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
    {
        var lookup = new Dictionary<Guid, GameObject>();

        static void AddObject(GameObject obj, IDictionary<Guid, GameObject> map)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                map.TryAdd(obj.ObjectId, obj);
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddObject(child, map);
            }
        }

        foreach (var template in project.ObjectTemplates)
        {
            AddObject(template, lookup);
        }

        foreach (var baseObject in project.BaseObjects)
        {
            AddObject(baseObject, lookup);
        }

        foreach (var playerObject in project.GlobalScope.GameObjects)
        {
            AddObject(playerObject, lookup);
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
            AddObject(scopedObject, lookup);
        }

        foreach (var roomObject in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms)
                     .SelectMany(room => room.GameObjects))
        {
            AddObject(roomObject, lookup);
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
            AddObject(scopedBaseObject, lookup);
        }

        return lookup;
    }
}