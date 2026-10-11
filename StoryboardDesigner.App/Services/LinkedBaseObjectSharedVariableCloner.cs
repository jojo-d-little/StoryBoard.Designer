using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

internal static class LinkedBaseObjectSharedVariableCloner
{
    public static void PreserveAuthoredMemberships(ProjectModel project, GameObject source, GameObject clone)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(clone);

        if (!IsAuthoredBaseObject(project, source))
        {
            return;
        }

        SharedVariableReconciliationService.ReconcileInPlace(project);

        var sourceObjectIds = EnumerateObjects(source)
            .Select(static gameObject => gameObject.ObjectId)
            .Where(static objectId => objectId != Guid.Empty)
            .ToHashSet();
        var objectsById = EnumerateProjectObjects(project)
            .Where(static gameObject => gameObject.ObjectId != Guid.Empty)
            .GroupBy(static gameObject => gameObject.ObjectId)
            .ToDictionary(static group => group.Key, static group => group.First());
        var cloneVariablesBySourceShareId = new Dictionary<Guid, List<GamePropertyDefinition>>();

        CollectCloneVariables(source, clone);

        foreach (var (sourceShareId, clonedVariables) in cloneVariablesBySourceShareId)
        {
            var shared = project.SharedVariables.FirstOrDefault(candidate => candidate.Id == sourceShareId);
            var hasAuthoredOutsideParticipant = shared?.Participants.Any(participant =>
                !IsParticipantInsideSource(participant, sourceObjectIds)
                && !IsLinkedPlacementDerivativeOfSource(participant, sourceObjectIds, objectsById)) == true;

            var clonedShareId = hasAuthoredOutsideParticipant ? sourceShareId : Guid.NewGuid();
            foreach (var variable in clonedVariables)
            {
                variable.SharedVariableId = clonedShareId;
            }
        }

        void CollectCloneVariables(GameObject original, GameObject copied)
        {
            var variableCount = Math.Min(original.Variables.Count, copied.Variables.Count);
            for (var index = 0; index < variableCount; index++)
            {
                var sourceVariable = original.Variables[index];
                if (sourceVariable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
                {
                    continue;
                }

                if (!cloneVariablesBySourceShareId.TryGetValue(sharedId, out var clonedVariables))
                {
                    clonedVariables = new List<GamePropertyDefinition>();
                    cloneVariablesBySourceShareId[sharedId] = clonedVariables;
                }

                clonedVariables.Add(copied.Variables[index]);
            }

            var childCount = Math.Min(original.ContainedObjects.Count, copied.ContainedObjects.Count);
            for (var index = 0; index < childCount; index++)
            {
                CollectCloneVariables(original.ContainedObjects[index], copied.ContainedObjects[index]);
            }
        }
    }

    private static bool IsParticipantInsideSource(SharedVariableParticipant participant, ISet<Guid> sourceObjectIds)
    {
        return string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase)
            && sourceObjectIds.Contains(participant.OwnerId);
    }

    private static bool IsLinkedPlacementDerivativeOfSource(
        SharedVariableParticipant participant,
        ISet<Guid> sourceObjectIds,
        IReadOnlyDictionary<Guid, GameObject> objectsById)
    {
        if (!string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase)
            || !objectsById.TryGetValue(participant.OwnerId, out var current))
        {
            return false;
        }

        var visited = new HashSet<Guid>();
        while (current.LinkedBaseObjectId is Guid linkedBaseObjectId
               && linkedBaseObjectId != Guid.Empty
               && visited.Add(current.ObjectId))
        {
            if (sourceObjectIds.Contains(linkedBaseObjectId))
            {
                return true;
            }

            if (!objectsById.TryGetValue(linkedBaseObjectId, out var linkedObject))
            {
                break;
            }

            current = linkedObject;
        }

        return false;
    }

    private static bool IsAuthoredBaseObject(ProjectModel project, GameObject source)
    {
        return EnumerateBaseObjectRoots(project)
            .SelectMany(EnumerateObjects)
            .Any(candidate => ReferenceEquals(candidate, source));
    }

    private static IEnumerable<GameObject> EnumerateBaseObjectRoots(ProjectModel project)
    {
        foreach (var root in project.BaseObjects)
        {
            yield return root;
        }

        foreach (var planet in project.Planets)
        {
            foreach (var root in planet.BaseObjects)
            {
                yield return root;
            }

            foreach (var country in planet.Countries)
            {
                foreach (var root in country.BaseObjects)
                {
                    yield return root;
                }

                foreach (var area in country.Areas)
                {
                    foreach (var root in area.BaseObjects)
                    {
                        yield return root;
                    }
                }
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateProjectObjects(ProjectModel project)
    {
        foreach (var root in project.GlobalScope.GameObjects
                     .Concat(project.EnumerateAllObjectTemplateRoots())
                     .Concat(EnumerateBaseObjectRoots(project))
                     .Concat(project.EnumerateAllRoomTemplateRoots().SelectMany(static room => room.GameObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.GameObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries)
                         .SelectMany(static country => country.GameObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries)
                         .SelectMany(static country => country.Areas)
                         .SelectMany(static area => area.GameObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries)
                         .SelectMany(static country => country.Areas)
                         .SelectMany(static area => area.Rooms)
                         .SelectMany(static room => room.GameObjects)))
        {
            foreach (var gameObject in EnumerateObjects(root))
            {
                yield return gameObject;
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateObjects(GameObject root)
    {
        yield return root;
        foreach (var child in root.ContainedObjects)
        {
            foreach (var descendant in EnumerateObjects(child))
            {
                yield return descendant;
            }
        }
    }
}
