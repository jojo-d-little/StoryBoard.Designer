using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

internal static class ActionRuleSupport
{
    internal static string BuildActionIssuePath(string scopePath, CommandAction action)
    {
        var prefix = string.IsNullOrWhiteSpace(scopePath) ? "Project" : scopePath.Trim();
        var actionLabel = string.IsNullOrWhiteSpace(action.Name) ? "(unnamed action)" : action.Name.Trim();
        return $"{prefix} / Action:{actionLabel}";
    }

    internal static bool TryGetCandidateContext(
        ValidationRuleContext ruleContext,
        out CandidateActionValidationContext context)
    {
        if (ruleContext.CandidateNode is not ValidationActionCandidateNode actionCandidate)
        {
            context = null!;
            return false;
        }

        var objectLookup = ruleContext.SharedState.GetOrAdd(
            "ACT:ObjectLookup",
            () => BuildObjectLookup(ruleContext.Project));

        var globalActionTargets = ruleContext.SharedState.GetOrAdd(
            "ACT:GlobalActionTargets",
            () => ruleContext.Project.GlobalScope.GameObjects.SelectMany(obj => obj.AvailableActions).ToList());

        var actions = actionCandidate.OwnerScope switch
        {
            Planet planet => planet.AvailableActions.ToList(),
            Country country => country.AvailableActions.ToList(),
            Area area => area.AvailableActions.ToList(),
            Room room => room.AvailableActions.ToList(),
            GameObject gameObject => ResolveEffectiveObjectActions(gameObject, objectLookup).ToList(),
            _ => new List<CommandAction>()
        };

        if (actions.All(action => action.Id != actionCandidate.Action.Id))
        {
            actions.Add(actionCandidate.Action);
        }

        var actionById = actions
            .GroupBy(action => action.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var linkTargetById = actions
            .GroupBy(action => action.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var action in globalActionTargets)
        {
            linkTargetById.TryAdd(action.Id, action);
        }

        context = new CandidateActionValidationContext(
            actionCandidate.OwnerScopePath,
            actionCandidate.Action,
            actions,
            actionById,
            linkTargetById);

        return true;
    }

    internal static IEnumerable<ActionValidationContext> EnumerateActionContexts(ProjectModel project)
    {
        var objectLookup = BuildObjectLookup(project);
        var globalActionTargets = project.GlobalScope.GameObjects.SelectMany(obj => obj.AvailableActions).ToList();

        foreach (var globalObject in project.GlobalScope.GameObjects)
        {
            var localActions = globalObject.AvailableActions.ToList();
            var additionalTargets = globalActionTargets
                .Where(action => localActions.All(local => local.Id != action.Id))
                .ToList();

            yield return CreateContext($"Global / {globalObject.Name}", localActions, additionalTargets);
        }

        foreach (var planet in project.Planets)
        {
            yield return CreateContext($"{planet.Name}", planet.AvailableActions, globalActionTargets);

            foreach (var obj in planet.GameObjects)
            {
                var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                yield return CreateContext(
                    $"{planet.Name} / {obj.Name}",
                    effectiveActions,
                    globalActionTargets);
            }

            foreach (var country in planet.Countries)
            {
                yield return CreateContext($"{planet.Name} / {country.Name}", country.AvailableActions, globalActionTargets);

                foreach (var obj in country.GameObjects)
                {
                    var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                    yield return CreateContext(
                        $"{planet.Name} / {country.Name} / {obj.Name}",
                        effectiveActions,
                        globalActionTargets);
                }

                foreach (var area in country.Areas)
                {
                    yield return CreateContext($"{planet.Name} / {country.Name} / {area.Name}", area.AvailableActions, globalActionTargets);

                    foreach (var obj in area.GameObjects)
                    {
                        var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                        yield return CreateContext(
                            $"{planet.Name} / {country.Name} / {area.Name} / {obj.Name}",
                            effectiveActions,
                            globalActionTargets);
                    }

                    foreach (var room in area.Rooms)
                    {
                        yield return CreateContext($"{planet.Name} / {country.Name} / {area.Name} / {room.Name}", room.AvailableActions, globalActionTargets);

                        foreach (var obj in room.GameObjects)
                        {
                            var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                            yield return CreateContext(
                                $"{planet.Name} / {country.Name} / {area.Name} / {room.Name} / {obj.Name}",
                                effectiveActions,
                                globalActionTargets);
                        }
                    }
                }
            }
        }
    }

    private static ActionValidationContext CreateContext(
        string path,
        IEnumerable<CommandAction> actions,
        IEnumerable<CommandAction>? additionalLinkTargetActions)
    {
        var actionList = actions.ToList();
        var actionById = actionList
            .GroupBy(action => action.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var linkTargetById = actionList
            .GroupBy(action => action.Id)
            .ToDictionary(group => group.Key, group => group.First());

        if (additionalLinkTargetActions is not null)
        {
            foreach (var action in additionalLinkTargetActions)
            {
                linkTargetById.TryAdd(action.Id, action);
            }
        }

        return new ActionValidationContext(path, actionList, actionById, linkTargetById);
    }

    private static IReadOnlyDictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
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

        return lookup;
    }

    private static IReadOnlyList<CommandAction> ResolveEffectiveObjectActions(
        GameObject obj,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        if (!obj.LinkActionsToBaseObject || !obj.LinkedBaseObjectId.HasValue)
        {
            return obj.AvailableActions;
        }

        var visited = new HashSet<Guid>();
        var current = obj;

        while (current.LinkActionsToBaseObject && current.LinkedBaseObjectId.HasValue)
        {
            var baseId = current.LinkedBaseObjectId.Value;
            if (!visited.Add(baseId)
                || !objectLookup.TryGetValue(baseId, out var baseObject)
                || ReferenceEquals(baseObject, current))
            {
                break;
            }

            current = baseObject;
        }

        return current.AvailableActions;
    }
}
