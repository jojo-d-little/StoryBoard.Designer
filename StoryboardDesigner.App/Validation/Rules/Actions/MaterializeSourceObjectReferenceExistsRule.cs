using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class MaterializeSourceObjectReferenceExistsRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-014", "Materialize Source Object Reference Exists", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        if (action.ActionType != CommandActionType.MaterializeObjectCopy)
        {
            yield break;
        }

        var sourceObjectId = ActionPayloadAccessors.GetMaterializeSourceObjectId(action);
        if (!sourceObjectId.HasValue || sourceObjectId.Value == Guid.Empty)
        {
            yield break;
        }

        var knownObjectIds = ruleContext.SharedState.GetOrAdd(
            "ACT:KnownProjectObjectIds",
            () => BuildKnownObjectIdSet(ruleContext.Project));

        if (knownObjectIds.Contains(sourceObjectId.Value))
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            $"MaterializeObjectCopy action '{action.Name}' references source object id '{sourceObjectId.Value:N}' that does not exist in the project.",
            "Select an existing source object for this action in the action editor.");
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

        foreach (var roomTemplateObject in project.RoomTemplates.SelectMany(room => room.GameObjects))
        {
            AddObject(roomTemplateObject, known);
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