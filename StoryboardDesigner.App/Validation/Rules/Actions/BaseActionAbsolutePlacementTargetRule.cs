using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

/// <summary>
/// A placed object's ID is an absolute target even when that placement links to this base.
/// </summary>
public sealed class BaseActionAbsolutePlacementTargetRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-021",
        "Base Action Targets a Concrete Placement",
        ValidationSeverity.Warning,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public bool SupportsActionCandidates => false;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not GameObject owner)
        {
            yield break;
        }

        var baseRootByObjectId = context.SharedState.GetOrAdd(
            "ACT:BaseRootByObjectId",
            () => EnumerateBaseRoots(context.Project)
                .SelectMany(root => EnumerateObjects(root).Select(obj => (obj.ObjectId, Root: root)))
                .Where(static pair => pair.ObjectId != Guid.Empty)
                .GroupBy(static pair => pair.ObjectId)
                .ToDictionary(static group => group.Key, static group => group.First().Root));
        if (!baseRootByObjectId.TryGetValue(owner.ObjectId, out var baseRoot))
        {
            yield break;
        }

        var baseIds = EnumerateObjects(baseRoot).Select(static obj => obj.ObjectId).ToHashSet();
        var roomRoots = context.Project.Planets
            .SelectMany(static planet => planet.Countries)
            .SelectMany(static country => country.Areas)
            .SelectMany(static area => area.Rooms)
            .SelectMany(static room => room.GameObjects)
            .ToList();
        var placedById = context.SharedState.GetOrAdd(
            "ACT:PlacedObjectsById",
            () => roomRoots
                .SelectMany(EnumerateObjects)
                .Where(static obj => obj.ObjectId != Guid.Empty)
                .GroupBy(static obj => obj.ObjectId)
                .ToDictionary(static group => group.Key, static group => group.First()));

        foreach (var action in owner.AvailableActions)
        {
            foreach (var issue in Check(action, $"{owner.ScopeName} / Action:{action.Name}"))
            {
                yield return issue;
            }
        }

        foreach (var sequence in owner.SequenceDefinitions)
        {
            foreach (var step in sequence.Steps ?? [])
            {
                if (step.InlineAction is null)
                {
                    continue;
                }

                var action = JsonExportService.ToCommandActionModel(step.InlineAction);
                foreach (var issue in Check(action, $"{owner.ScopeName} / Sequence:{sequence.DisplayName} / Step:{step.StepId:D} / Action:{action.Name}"))
                {
                    yield return issue;
                }
            }
        }

        IEnumerable<ValidationIssue> Check(CommandAction action, string path)
        {
            if (action.ActionType is not (CommandActionType.AddObjectPresentationCue
                or CommandActionType.RemoveObjectPresentationCue
                or CommandActionType.ClearObjectPresentationCues)
                || action.ObjectPresentationCueObjectId is not { } targetId
                || !placedById.TryGetValue(targetId, out var placed))
            {
                yield break;
            }

            var sourceId = ResolveCorrespondingBaseId(placed);
            if (!sourceId.HasValue)
            {
                yield break;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                path,
                $"Action '{action.Name}' targets placed object '{placed.Name}' ({targetId:D}) by its absolute ID. It will not follow each instance of base '{baseRoot.Name}'.",
                $"For current-instance intent, choose the corresponding base object ID '{sourceId.Value:D}'. Keep the placed ID only when an absolute target is intended.");
        }

        Guid? ResolveCorrespondingBaseId(GameObject placed)
        {
            if (placed.LinkedBaseObjectId is { } sourceId && baseIds.Contains(sourceId))
            {
                return sourceId;
            }

            foreach (var roomRoot in roomRoots.Where(root => root.LinkedBaseObjectId == baseRoot.ObjectId))
            {
                if (!TryFindChildPath(roomRoot, placed.ObjectId, out var path))
                {
                    continue;
                }

                var source = baseRoot;
                foreach (var index in path)
                {
                    if (index >= source.ContainedObjects.Count)
                    {
                        source = null!;
                        break;
                    }
                    source = source.ContainedObjects[index];
                }
                if (source is not null)
                {
                    return source.ObjectId;
                }
            }

            return null;
        }
    }

    private static bool TryFindChildPath(GameObject root, Guid targetId, out List<int> path)
    {
        path = new List<int>();
        if (root.ObjectId == targetId)
        {
            return true;
        }

        for (var index = 0; index < root.ContainedObjects.Count; index++)
        {
            if (TryFindChildPath(root.ContainedObjects[index], targetId, out var childPath))
            {
                path.Add(index);
                path.AddRange(childPath);
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<GameObject> EnumerateBaseRoots(ProjectModel project) =>
        project.BaseObjects
            .Concat(project.Planets.SelectMany(static planet => planet.BaseObjects))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.BaseObjects))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries)
                .SelectMany(static country => country.Areas).SelectMany(static area => area.BaseObjects));

    private static IEnumerable<GameObject> EnumerateObjects(GameObject root)
    {
        yield return root;
        foreach (var child in root.ContainedObjects.SelectMany(EnumerateObjects))
        {
            yield return child;
        }
    }
}
