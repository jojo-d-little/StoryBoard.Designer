using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class ObjectPresentationCueTargetReferenceExistsRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-017",
        "Object Presentation Cue Target Reference Exists",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global, ScopeNodeKind.GameObject };
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (ruleContext.CandidateNode is ProjectModel project)
        {
            var knownObjectIds = GetKnownObjectIds(ruleContext);
            foreach (var ownerScope in EnumerateSequenceOwners(project))
            {
                var ownerPath = string.Join(" / ", ownerScope.EnumerateSelfAndAncestors()
                    .Reverse()
                    .Select(static scope => scope.ScopeName)
                    .Where(static name => !string.IsNullOrWhiteSpace(name)));
                foreach (var issue in CheckInlineSequenceTargets(ownerScope, knownObjectIds, ownerPath))
                {
                    yield return issue;
                }
            }

            yield break;
        }

        if (ruleContext.CandidateNode is GameObject owner)
        {
            // Whole-project validation handles every sequence from the project root;
            // retain this candidate path for scoped validation without duplicating issues.
            if (ruleContext.IsFullProject)
            {
                yield break;
            }

            var knownObjectIds = GetKnownObjectIds(ruleContext);
            var ownerPath = string.Join(" / ", owner.EnumerateSelfAndAncestors()
                .Reverse()
                .Select(static scope => scope.ScopeName)
                .Where(static name => !string.IsNullOrWhiteSpace(name)));

            foreach (var issue in CheckInlineSequenceTargets(owner, knownObjectIds, ownerPath))
            {
                yield return issue;
            }
            yield break;
        }

        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        foreach (var issue in CheckTarget(context.Action, GetKnownObjectIds(ruleContext), context.Path))
        {
            yield return issue;
        }
    }

    private IEnumerable<ValidationIssue> CheckTarget(CommandAction action, HashSet<Guid> knownObjectIds, string path)
    {
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

        if (knownObjectIds.Contains(targetObjectId.Value))
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(path, action),
            $"{action.ActionType} action '{action.Name}' references object id '{targetObjectId.Value:D}' that does not exist in the project.",
            "Choose an existing target object, or clear the explicit target to use the primary command object.");
    }

    private static HashSet<Guid> GetKnownObjectIds(ValidationRuleContext ruleContext)
    {
        return ruleContext.SharedState.GetOrAdd(
            "ACT:KnownPresentationCueTargetObjectIds",
            () => BuildKnownObjectIdSet(ruleContext.Project));
    }

    private IEnumerable<ValidationIssue> CheckInlineSequenceTargets(
        ScopeNodeBase ownerScope,
        HashSet<Guid> knownObjectIds,
        string ownerPath)
    {
        foreach (var sequence in ownerScope.SequenceDefinitions)
        {
            foreach (var step in sequence.Steps ?? [])
            {
                if (step.InlineAction is null)
                {
                    continue;
                }

                var inlineAction = JsonExportService.ToCommandActionModel(step.InlineAction);
                foreach (var issue in CheckTarget(inlineAction, knownObjectIds,
                             $"{ownerPath} / Sequence:{sequence.DisplayName} / Step:{step.StepId:D}"))
                {
                    yield return issue;
                }
            }
        }
    }

    private static IEnumerable<ScopeNodeBase> EnumerateSequenceOwners(ProjectModel project)
    {
        var visited = new HashSet<ScopeNodeBase>(ReferenceEqualityComparer.Instance);

        IEnumerable<ScopeNodeBase> Visit(ScopeNodeBase scope)
        {
            if (!visited.Add(scope))
            {
                yield break;
            }

            yield return scope;
            foreach (var child in scope.ChildScopes.OfType<ScopeNodeBase>())
            {
                foreach (var nested in Visit(child))
                {
                    yield return nested;
                }
            }
        }

        foreach (var scope in Visit(project))
        {
            yield return scope;
        }

        var regionalBaseRoots = project.BaseObjects
            .Concat(project.Planets.SelectMany(static planet => planet.BaseObjects))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.BaseObjects))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries)
                .SelectMany(static country => country.Areas).SelectMany(static area => area.BaseObjects));

        foreach (var root in regionalBaseRoots)
        {
            foreach (var scope in Visit(root))
            {
                yield return scope;
            }
        }
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
