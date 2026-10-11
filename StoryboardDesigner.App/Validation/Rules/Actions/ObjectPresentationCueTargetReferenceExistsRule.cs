using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class ObjectPresentationCueTargetReferenceExistsRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-017",
        "Object Presentation Cue Target Reference Exists",
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
        if (action.ActionType is not (CommandActionType.AddObjectPresentationCue
            or CommandActionType.RemoveObjectPresentationCue
            or CommandActionType.ClearObjectPresentationCues))
        {
            yield break;
        }

        var targetObjectId = action.ObjectPresentationCueObjectId;
        if (!targetObjectId.HasValue || targetObjectId.Value == Guid.Empty)
        {
            yield break;
        }

        var knownObjectIds = ruleContext.SharedState.GetOrAdd(
            "ACT:KnownPresentationCueTargetObjectIds",
            () => BuildKnownObjectIdSet(ruleContext.Project));

        if (knownObjectIds.Contains(targetObjectId.Value))
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            $"{action.ActionType} action '{action.Name}' references object id '{targetObjectId.Value:D}' that does not exist in the project.",
            "Choose an existing target object, or clear the explicit target to use the primary command object.");
    }

    private static HashSet<Guid> BuildKnownObjectIdSet(ProjectModel project)
    {
        var known = new HashSet<Guid>();

        static void AddObjects(IEnumerable<GameObject> roots, ISet<Guid> ids)
        {
            foreach (var obj in roots)
            {
                if (obj.ObjectId != Guid.Empty)
                {
                    ids.Add(obj.ObjectId);
                }

                AddObjects(obj.ContainedObjects, ids);
            }
        }

        AddObjects(project.EnumerateAllObjectTemplateRoots(), known);
        AddObjects(project.EnumerateAllRoomTemplateRoots().SelectMany(static room => room.GameObjects), known);
        AddObjects(project.BaseObjects, known);
        AddObjects(project.GlobalScope.GameObjects, known);

        foreach (var planet in project.Planets)
        {
            AddObjects(planet.GameObjects, known);
            AddObjects(planet.BaseObjects, known);

            foreach (var country in planet.Countries)
            {
                AddObjects(country.GameObjects, known);
                AddObjects(country.BaseObjects, known);

                foreach (var area in country.Areas)
                {
                    AddObjects(area.GameObjects, known);
                    AddObjects(area.BaseObjects, known);

                    foreach (var room in area.Rooms)
                    {
                        AddObjects(room.GameObjects, known);
                    }
                }
            }
        }

        return known;
    }
}
