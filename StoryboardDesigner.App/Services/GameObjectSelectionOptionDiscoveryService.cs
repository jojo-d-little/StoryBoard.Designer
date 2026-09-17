using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed class GameObjectSelectionOptionDiscoveryService : IGameObjectSelectionOptionDiscoveryService
{
    public IReadOnlyList<GameObjectSelectionOption> Discover(GameObjectSelectionOptionDiscoveryRequest request)
    {
        if (request.CurrentObject is null)
        {
            return Array.Empty<GameObjectSelectionOption>();
        }

        var sourceType = request.ScopeSearchType;

        var candidates = new List<GameObject>();

        var scopeTargets = request.ScopeSearchDepth == ScopeSearchDepth.None
            ? ScopeSearchDepth.LocalRoom
            : request.ScopeSearchDepth;

        if ((scopeTargets & ScopeSearchDepth.LocalRoom) != 0)
        {
            PopulateLocalCandidates(request.CurrentObject, candidates, sourceType, request.RequiredFeatures);
        }

        if ((scopeTargets & ScopeSearchDepth.Ancestors) != 0)
        {
            PopulateAncestorCandidates(request.CurrentObject, candidates, sourceType, request.RequiredFeatures);
        }

        if ((scopeTargets & ScopeSearchDepth.Children) != 0)
        {
            PopulateChildrenCandidates(request.CurrentObject, candidates, sourceType, request.RequiredFeatures);
        }

        if ((scopeTargets & ScopeSearchDepth.Project) != 0)
        {
            PopulateGlobalCandidates(request.CurrentObject, candidates, sourceType, request.RequiredFeatures);
        }

        var listRequest = new GameObjectOptionListRequest(
            Candidates: candidates,
            CurrentObject: request.CurrentObject,
            ExcludeCurrentObject: request.ExcludeCurrentObject);

        return GameObjectSelectionOptionNormalizer.Build(listRequest);
    }

    private static void PopulateLocalCandidates(
        GameObject currentObject,
        ICollection<GameObject> candidates,
        GameObjectOptionSourceTarget sourceType,
        GameObjectFeatureRequirements requiredFeatures)
    {
        var container = currentObject.ParentScope;
        if (container is null)
        {
            return;
        }

        IEnumerable<GameObject> siblings = sourceType switch
        {
            GameObjectOptionSourceTarget.RealObjects => EnumerateRealLocalCandidates(container),
            GameObjectOptionSourceTarget.ObjectTemplates => EnumerateTemplateLocalCandidates(container),
            _ => Array.Empty<GameObject>()
        };

        foreach (var obj in siblings)
        {
            if (!MatchesRequiredFeatures(obj, requiredFeatures)
                || !MatchesSourceType(obj, sourceType))
            {
                continue;
            }

            candidates.Add(obj);
        }
    }

    private static void PopulateAncestorCandidates(
        GameObject currentObject,
        ICollection<GameObject> candidates,
        GameObjectOptionSourceTarget sourceType,
        GameObjectFeatureRequirements requiredFeatures)
    {
        foreach (var ancestor in currentObject.EnumerateAncestors().OfType<GameObject>())
        {
            if (!MatchesRequiredFeatures(ancestor, requiredFeatures)
                || !MatchesSourceType(ancestor, sourceType))
            {
                continue;
            }

            candidates.Add(ancestor);
        }
    }

    private static void PopulateGlobalCandidates(
        GameObject currentObject,
        ICollection<GameObject> candidates,
        GameObjectOptionSourceTarget sourceType,
        GameObjectFeatureRequirements requiredFeatures)
    {
        var root = FindRootScopeNode(currentObject);
        if (root is null)
        {
            return;
        }

        var source = sourceType == GameObjectOptionSourceTarget.RealObjects
            ? EnumerateScopeTreeRealGameObjects(root)
            : EnumerateScopeTreeGameObjects(root);

        foreach (var obj in source)
        {
            if (!MatchesRequiredFeatures(obj, requiredFeatures)
                || !MatchesSourceType(obj, sourceType))
            {
                continue;
            }

            candidates.Add(obj);
        }
    }

    private static void PopulateChildrenCandidates(
        GameObject currentObject,
        ICollection<GameObject> candidates,
        GameObjectOptionSourceTarget sourceType,
        GameObjectFeatureRequirements requiredFeatures)
    {
        foreach (var child in EnumerateGameObjectsRecursive(currentObject.ContainedObjects))
        {
            if (!MatchesRequiredFeatures(child, requiredFeatures)
                || !MatchesSourceType(child, sourceType))
            {
                continue;
            }

            candidates.Add(child);
        }
    }

    private static IEnumerable<GameObject> EnumerateRealLocalCandidates(IScopedAwareNode container)
    {
        if (container is ProjectModel project)
        {
            return EnumerateGameObjectsRecursive(project.GlobalScope.GameObjects);
        }

        return container switch
        {
            Room room => EnumerateGameObjectsRecursive(room.GameObjects),
            GameObject parentObject => EnumerateGameObjectsRecursive(parentObject.ContainedObjects),
            _ => Array.Empty<GameObject>()
        };
    }

    private static IEnumerable<GameObject> EnumerateTemplateLocalCandidates(IScopedAwareNode container)
    {
        return container switch
        {
            GameObject parentObject => EnumerateGameObjectsRecursive(parentObject.ContainedObjects),
            ObjectTemplatesScopeNode objectTemplates => EnumerateGameObjectsRecursive(objectTemplates.ChildScopes.OfType<GameObject>()),
            BaseObjectsScopeNode baseObjects => EnumerateGameObjectsRecursive(baseObjects.ChildScopes.OfType<GameObject>()),
            RoomTemplatesScopeNode roomTemplates => roomTemplates.ChildScopes
                .OfType<Room>()
                .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects)),
            Planet planet => EnumerateGameObjectsRecursive(planet.BaseObjects),
            Country country => EnumerateGameObjectsRecursive(country.BaseObjects),
            Area area => EnumerateGameObjectsRecursive(area.BaseObjects),
            Room room when IsTemplateRoom(room) => EnumerateGameObjectsRecursive(room.GameObjects),
            _ => Array.Empty<GameObject>()
        };
    }

    private static ScopeNodeBase? FindRootScopeNode(GameObject currentObject)
    {
        ScopeNodeBase? root = currentObject;
        var cursor = currentObject.ParentScope;
        while (cursor is ScopeNodeBase node)
        {
            root = node;
            cursor = node.ParentScope;
        }

        return root;
    }

    private static IEnumerable<GameObject> EnumerateScopeTreeGameObjects(IScopedAwareNode root)
    {
        if (root is GameObject obj)
        {
            yield return obj;
        }

        if (root is Planet planet)
        {
            foreach (var baseObject in EnumerateGameObjectsRecursive(planet.BaseObjects))
            {
                yield return baseObject;
            }
        }

        if (root is Country country)
        {
            foreach (var baseObject in EnumerateGameObjectsRecursive(country.BaseObjects))
            {
                yield return baseObject;
            }
        }

        if (root is Area area)
        {
            foreach (var baseObject in EnumerateGameObjectsRecursive(area.BaseObjects))
            {
                yield return baseObject;
            }
        }

        foreach (var child in root.ChildScopes)
        {
            foreach (var nested in EnumerateScopeTreeGameObjects(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateScopeTreeRealGameObjects(IScopedAwareNode root)
    {
        if (root is GameObject obj)
        {
            yield return obj;

            foreach (var nested in EnumerateGameObjectsRecursive(obj.ContainedObjects))
            {
                yield return nested;
            }

            yield break;
        }

        if (root is Room room && !IsTemplateRoom(room))
        {
            foreach (var roomObject in EnumerateGameObjectsRecursive(room.GameObjects))
            {
                yield return roomObject;
            }
        }

        foreach (var child in root.ChildScopes)
        {
            if (child is ObjectTemplatesScopeNode or BaseObjectsScopeNode or RoomTemplatesScopeNode)
            {
                continue;
            }

            foreach (var nested in EnumerateScopeTreeRealGameObjects(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsTemplateRoom(Room room)
    {
        var current = room as IScopedAwareNode;
        while (current is not null)
        {
            if (current.ParentScope is RoomTemplatesScopeNode)
            {
                return true;
            }

            current = current.ParentScope;
        }

        return false;
    }

    private static bool MatchesSourceType(GameObject obj, GameObjectOptionSourceTarget sourceType)
    {
        var isTemplate = IsTemplateObject(obj);
        return sourceType == GameObjectOptionSourceTarget.ObjectTemplates
            ? isTemplate
            : !isTemplate;
    }

    private static bool IsTemplateObject(GameObject obj)
    {
        var current = obj as IScopedAwareNode;
        while (current is not null)
        {
            var parent = current.ParentScope;
            if (parent is null)
            {
                return false;
            }

            if (parent is ObjectTemplatesScopeNode or BaseObjectsScopeNode or RoomTemplatesScopeNode)
            {
                return true;
            }

            if (parent is Planet planet && ReferenceEquals(current, obj) && planet.BaseObjects.Contains(obj))
            {
                return true;
            }

            if (parent is Country country && ReferenceEquals(current, obj) && country.BaseObjects.Contains(obj))
            {
                return true;
            }

            if (parent is Area area && ReferenceEquals(current, obj) && area.BaseObjects.Contains(obj))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static bool MatchesRequiredFeatures(GameObject obj, GameObjectFeatureRequirements requiredFeatures)
    {
        if (requiredFeatures == GameObjectFeatureRequirements.None)
        {
            return true;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Inventoriable) && !obj.IsInventoriable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Openable) && !obj.IsOpenable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Lockable) && !obj.IsLockable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Container) && !obj.IsContainer)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Activatable) && !obj.IsActivatable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Hidable) && !obj.IsHidable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Quantifiable) && !obj.IsQuantifiable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.CompositeTarget) && !obj.IsCompositeTarget)
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<GameObject> EnumerateGameObjectsRecursive(IEnumerable<GameObject> rootObjects)
    {
        foreach (var gameObject in rootObjects)
        {
            yield return gameObject;

            foreach (var nested in EnumerateGameObjectsRecursive(gameObject.ContainedObjects))
            {
                yield return nested;
            }
        }
    }

}
