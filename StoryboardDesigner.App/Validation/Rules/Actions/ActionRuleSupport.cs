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

    internal static string DescribeMissingLinkTarget(
        ProjectModel project,
        CandidateActionValidationContext context,
        ScopeNodeBase ownerScope,
        Guid actionId,
        Guid? targetObjectId)
    {
        var matches = EnumerateActionDefinitions(project)
            .Where(definition => definition.Action.Id == actionId)
            .Select(definition => $"{definition.ScopePath} / Action:{definition.Action.Name} ({definition.Action.ActionType})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        var matchDescription = matches.Count == 0
            ? "No action definition with this ID was found anywhere in the project."
            : $"Project-wide definition(s) with this ID: {string.Join("; ", matches)}";
        var room = ownerScope as Room
            ?? ownerScope.EnumerateSelfAndAncestors().OfType<Room>().FirstOrDefault();
        var lookupDescription = targetObjectId.HasValue
            ? "Because this node has an explicit TargetObjectId, validation looks for this action on that exact object and its linked-base-object definition."
            : room is null
                ? "This node has no explicit TargetObjectId. Eligible lookup includes actions on the owner and ancestor scopes, plus Global objects."
                : $"This node has no explicit TargetObjectId. Eligible lookup includes actions on the owner and ancestor scopes, objects placed in room '{room.Name}' (including inherited linked-base-object actions), plus Global objects.";
        var explicitTargetDescription = targetObjectId is Guid explicitObjectId
            ? DescribeExplicitObjectTarget(context, actionId, explicitObjectId)
            : "This node has no explicit TargetObjectId; lookup uses the eligible scopes described above.";

        return $"Lookup owner scope: '{context.Path}'. {lookupDescription} {explicitTargetDescription} {matchDescription}";
    }

    private static string DescribeExplicitObjectTarget(
        CandidateActionValidationContext context,
        Guid actionId,
        Guid targetObjectId)
    {
        if (!context.ObjectById.TryGetValue(targetObjectId, out var targetObject))
        {
            return $"The node has explicit TargetObjectId '{targetObjectId}', but that object ID is not present in the project model.";
        }

        var effectiveActions = context.ActionsByObjectId.TryGetValue(targetObjectId, out var actions)
            ? actions
            : new Dictionary<Guid, CommandAction>();
        if (effectiveActions.ContainsKey(actionId))
        {
            return $"The node targets object '{targetObject.Name}' [{targetObjectId}], and that object resolves this action ID.";
        }

        var availableActionNames = effectiveActions.Count == 0
            ? "(none)"
            : string.Join(", ", effectiveActions.Values.Select(static action => $"{action.Name} [{action.Id:N}]").Take(8));
        return $"The node has explicit TargetObjectId '{targetObjectId}' for object '{targetObject.Name}', but that object's effective actions do not contain action ID '{actionId}'. Available action(s): {availableActionNames}.";
    }

    private static IEnumerable<(string ScopePath, CommandAction Action)> EnumerateActionDefinitions(ProjectModel project)
    {
        static IEnumerable<(string ScopePath, CommandAction Action)> Actions(string scopePath, IEnumerable<CommandAction> actions)
        {
            foreach (var action in actions)
            {
                yield return ($"{scopePath} / Action:{action.Name}", action);
            }
        }

        static IEnumerable<(string ScopePath, CommandAction Action)> ObjectActions(
            string scopePath,
            IEnumerable<GameObject> objects)
        {
            foreach (var gameObject in objects)
            {
                var objectPath = $"{scopePath} / Object:{gameObject.Name} [{gameObject.ObjectId:N}]";
                foreach (var action in gameObject.AvailableActions)
                {
                    yield return (objectPath, action);
                }

                foreach (var child in ObjectActions(objectPath, gameObject.ContainedObjects))
                {
                    yield return child;
                }
            }
        }

        foreach (var action in Actions("Global", project.GlobalScope.AvailableActions))
        {
            yield return action;
        }

        foreach (var item in ObjectActions("Global", project.GlobalScope.GameObjects))
        {
            yield return item;
        }

        foreach (var item in ObjectActions("Project / Object templates", project.ObjectTemplates))
        {
            yield return item;
        }

        foreach (var item in ObjectActions("Project / Base objects", project.BaseObjects))
        {
            yield return item;
        }

        foreach (var planet in project.Planets)
        {
            foreach (var action in Actions($"Planet:{planet.Name}", planet.AvailableActions))
            {
                yield return action;
            }

            foreach (var item in ObjectActions($"Planet:{planet.Name}", planet.GameObjects.Concat(planet.ObjectTemplates)))
            {
                yield return item;
            }

            foreach (var item in ObjectActions($"Planet:{planet.Name} / Base objects", planet.BaseObjects))
            {
                yield return item;
            }

            foreach (var country in planet.Countries)
            {
                foreach (var action in Actions($"Planet:{planet.Name} / Country:{country.Name}", country.AvailableActions))
                {
                    yield return action;
                }

                foreach (var item in ObjectActions($"Planet:{planet.Name} / Country:{country.Name}", country.GameObjects.Concat(country.ObjectTemplates)))
                {
                    yield return item;
                }

                foreach (var item in ObjectActions($"Planet:{planet.Name} / Country:{country.Name} / Base objects", country.BaseObjects))
                {
                    yield return item;
                }

                foreach (var area in country.Areas)
                {
                    foreach (var action in Actions($"Planet:{planet.Name} / Country:{country.Name} / Area:{area.Name}", area.AvailableActions))
                    {
                        yield return action;
                    }

                    var areaPath = $"Planet:{planet.Name} / Country:{country.Name} / Area:{area.Name}";
                    foreach (var item in ObjectActions(areaPath, area.GameObjects.Concat(area.ObjectTemplates)))
                    {
                        yield return item;
                    }

                    foreach (var item in ObjectActions($"{areaPath} / Base objects", area.BaseObjects))
                    {
                        yield return item;
                    }

                    foreach (var room in area.Rooms)
                    {
                        var roomPath = $"{areaPath} / Room:{room.Name}";
                        foreach (var action in Actions(roomPath, room.AvailableActions))
                        {
                            yield return action;
                        }

                        foreach (var item in ObjectActions(roomPath, room.GameObjects))
                        {
                            yield return item;
                        }
                    }
                }
            }
        }
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

        var linkTargetsByOwnerScope = ruleContext.SharedState.GetOrAdd(
            "ACT:LinkTargetsByOwnerScope",
            () => new Dictionary<ScopeNodeBase, IReadOnlyList<CommandAction>>());

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

        if (!linkTargetsByOwnerScope.TryGetValue(actionCandidate.OwnerScope, out var additionalTargets))
        {
            additionalTargets = ResolveAdditionalLinkTargetActions(
                ruleContext.Project,
                actionCandidate.OwnerScope,
                actions,
                objectLookup);
            linkTargetsByOwnerScope[actionCandidate.OwnerScope] = additionalTargets;
        }

        foreach (var action in additionalTargets)
        {
            linkTargetById.TryAdd(action.Id, action);
        }

        context = new CandidateActionValidationContext(
            actionCandidate.OwnerScopePath,
            actionCandidate.Action,
            actions,
            actionById,
            linkTargetById,
            objectLookup,
            objectLookup.ToDictionary(
                static pair => pair.Key,
                pair => (IReadOnlyDictionary<Guid, CommandAction>)ResolveEffectiveObjectActions(pair.Value, objectLookup)
                    .Where(static action => action.Id != Guid.Empty)
                    .GroupBy(static action => action.Id)
                    .ToDictionary(static group => group.Key, static group => group.First())));

        return true;
    }

    internal static IEnumerable<ActionValidationContext> EnumerateActionContexts(ProjectModel project)
    {
        var objectLookup = BuildObjectLookup(project);

        foreach (var globalObject in project.GlobalScope.GameObjects)
        {
            var localActions = globalObject.AvailableActions.ToList();
            var additionalTargets = ResolveAdditionalLinkTargetActions(project, globalObject, localActions, objectLookup);

            yield return CreateContext($"Global / {globalObject.Name}", localActions, additionalTargets);
        }

        foreach (var planet in project.Planets)
        {
            yield return CreateContext(
                $"{planet.Name}",
                planet.AvailableActions,
                ResolveAdditionalLinkTargetActions(project, planet, planet.AvailableActions, objectLookup));

            foreach (var obj in planet.GameObjects)
            {
                var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                yield return CreateContext(
                    $"{planet.Name} / {obj.Name}",
                    effectiveActions,
                    ResolveAdditionalLinkTargetActions(project, obj, effectiveActions, objectLookup));
            }

            foreach (var country in planet.Countries)
            {
                yield return CreateContext(
                    $"{planet.Name} / {country.Name}",
                    country.AvailableActions,
                    ResolveAdditionalLinkTargetActions(project, country, country.AvailableActions, objectLookup));

                foreach (var obj in country.GameObjects)
                {
                    var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                    yield return CreateContext(
                        $"{planet.Name} / {country.Name} / {obj.Name}",
                        effectiveActions,
                        ResolveAdditionalLinkTargetActions(project, obj, effectiveActions, objectLookup));
                }

                foreach (var area in country.Areas)
                {
                    yield return CreateContext(
                        $"{planet.Name} / {country.Name} / {area.Name}",
                        area.AvailableActions,
                        ResolveAdditionalLinkTargetActions(project, area, area.AvailableActions, objectLookup));

                    foreach (var obj in area.GameObjects)
                    {
                        var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                        yield return CreateContext(
                            $"{planet.Name} / {country.Name} / {area.Name} / {obj.Name}",
                            effectiveActions,
                            ResolveAdditionalLinkTargetActions(project, obj, effectiveActions, objectLookup));
                    }

                    foreach (var room in area.Rooms)
                    {
                        yield return CreateContext(
                            $"{planet.Name} / {country.Name} / {area.Name} / {room.Name}",
                            room.AvailableActions,
                            ResolveAdditionalLinkTargetActions(project, room, room.AvailableActions, objectLookup));

                        foreach (var obj in room.GameObjects)
                        {
                            var effectiveActions = ResolveEffectiveObjectActions(obj, objectLookup);
                            yield return CreateContext(
                                $"{planet.Name} / {country.Name} / {area.Name} / {room.Name} / {obj.Name}",
                                effectiveActions,
                                ResolveAdditionalLinkTargetActions(project, obj, effectiveActions, objectLookup));
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

    private static IReadOnlyList<CommandAction> ResolveAdditionalLinkTargetActions(
        ProjectModel project,
        ScopeNodeBase ownerScope,
        IReadOnlyCollection<CommandAction> localActions,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        var targets = new List<CommandAction>();
        var seenActionIds = localActions
            .Where(static action => action.Id != Guid.Empty)
            .Select(static action => action.Id)
            .ToHashSet();

        void AddActions(IEnumerable<CommandAction> actions)
        {
            foreach (var action in actions)
            {
                if (action.Id != Guid.Empty && seenActionIds.Add(action.Id))
                {
                    targets.Add(action);
                }
            }
        }

        // Include actions from every enclosing scope, through the project scope.
        for (var scope = ownerScope.ParentScope; scope is not null; scope = scope.ParentScope)
        {
            AddActions(GetScopedActions(scope, objectLookup));
        }

        // A room action may link to actions on objects placed in that room. For an
        // object action, include the other object actions in its containing room.
        var room = ownerScope as Room
            ?? ownerScope.EnumerateSelfAndAncestors().OfType<Room>().FirstOrDefault();
        if (room is not null)
        {
            AddObjectActions(EnumerateGameObjects(room.GameObjects), objectLookup, AddActions);
        }

        // Preserve the existing global-object link targets, including nested objects.
        AddObjectActions(EnumerateGameObjects(project.GlobalScope.GameObjects), objectLookup, AddActions);
        return targets;
    }

    private static IEnumerable<CommandAction> GetScopedActions(
        IScopedAwareNode scope,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        return scope switch
        {
            ProjectModel project => project.GlobalScope.AvailableActions,
            Planet planet => planet.AvailableActions,
            Country country => country.AvailableActions,
            Area area => area.AvailableActions,
            Room room => room.AvailableActions,
            GameObject gameObject => ResolveEffectiveObjectActions(gameObject, objectLookup),
            _ => Array.Empty<CommandAction>()
        };
    }

    private static void AddObjectActions(
        IEnumerable<GameObject> objects,
        IReadOnlyDictionary<Guid, GameObject> objectLookup,
        Action<IEnumerable<CommandAction>> addActions)
    {
        foreach (var gameObject in objects)
        {
            addActions(ResolveEffectiveObjectActions(gameObject, objectLookup));
        }
    }

    private static IEnumerable<GameObject> EnumerateGameObjects(IEnumerable<GameObject> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var nested in EnumerateGameObjects(root.ContainedObjects))
            {
                yield return nested;
            }
        }
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

        foreach (var template in project.EnumerateAllObjectTemplateRoots())
        {
            AddObject(template, lookup);
        }

        foreach (var baseObject in project.BaseObjects
                     .Concat(project.Planets.SelectMany(static planet => planet.BaseObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.BaseObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.BaseObjects)))
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
