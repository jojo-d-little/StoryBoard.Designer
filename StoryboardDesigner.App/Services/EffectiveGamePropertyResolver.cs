using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class EffectiveGamePropertyResolver
{
    public sealed record EffectiveProperty(
        GamePropertyDefinition Definition,
        GameObject DefinitionOwner,
        bool IsInherited);

    public static IReadOnlyList<EffectiveProperty> Resolve(
        GameObject instance,
        Func<Guid, GameObject?> resolveBaseObject)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(resolveBaseObject);

        var baseObject = instance.LinkedBaseObjectId is Guid baseObjectId
            ? resolveBaseObject(baseObjectId)
            : null;
        if (baseObject is null || ReferenceEquals(baseObject, instance))
        {
            return instance.Variables
                .Select(property => new EffectiveProperty(property, instance, IsInherited: false))
                .ToList();
        }

        var instanceByName = instance.Variables
            .Where(static property => !string.IsNullOrWhiteSpace(property.Name))
            .GroupBy(static property => property.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var handledNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var instancePropertiesAdded = new HashSet<GamePropertyDefinition>();
        var result = new List<EffectiveProperty>();

        foreach (var baseProperty in baseObject.Variables)
        {
            var name = baseProperty.Name?.Trim() ?? string.Empty;
            if (name.Length > 0 && instanceByName.TryGetValue(name, out var instanceProperties))
            {
                if (handledNames.Add(name))
                {
                    foreach (var instanceProperty in instanceProperties)
                    {
                        result.Add(new EffectiveProperty(instanceProperty, instance, IsInherited: false));
                        instancePropertiesAdded.Add(instanceProperty);
                    }
                }
            }
            else
            {
                result.Add(new EffectiveProperty(baseProperty, baseObject, IsInherited: true));
            }
        }

        foreach (var instanceProperty in instance.Variables)
        {
            if (instancePropertiesAdded.Contains(instanceProperty))
            {
                continue;
            }

            result.Add(new EffectiveProperty(instanceProperty, instance, IsInherited: false));
        }

        return result;
    }

    public static GameObject? ResolveBaseObject(ProjectModel project, Guid objectId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (objectId == Guid.Empty)
        {
            return null;
        }

        return EnumerateBaseObjects(project)
            .FirstOrDefault(candidate => candidate.ObjectId == objectId);
    }

    public static IEnumerable<GameObject> EnumerateBaseObjects(ProjectModel project)
    {
        foreach (var root in project.BaseObjects
                     .Concat(project.Planets.SelectMany(static planet => planet.BaseObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.BaseObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.BaseObjects)))
        {
            foreach (var gameObject in EnumerateObjectTree(root))
            {
                yield return gameObject;
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateObjectTree(GameObject root)
    {
        yield return root;
        foreach (var child in root.ContainedObjects)
        {
            foreach (var descendant in EnumerateObjectTree(child))
            {
                yield return descendant;
            }
        }
    }
}
