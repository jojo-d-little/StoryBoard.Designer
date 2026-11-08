using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class PlacedBasePropertyValueRestrictionRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };
    public ValidationRuleMetadata Metadata { get; } = new("PROJ-021", "Placed Base Property Value Restriction", ValidationSeverity.Error, "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not ProjectModel project)
        {
            yield break;
        }

        foreach (var placement in EnumerateProjectObjects(project).Where(static gameObject => gameObject.LinkedBaseObjectId.HasValue))
        {
            foreach (var property in EffectiveGamePropertyResolver.Resolve(
                         placement,
                         id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id))
                     .Where(static property => property.IsBaseOwned && property.HasValueOverride))
            {
                if (VariableValueRestrictionValidator.IsAllowed(property.Definition.ValueRestriction, property.Definition.DefaultValue))
                {
                    continue;
                }

                var label = $"Placed object '{placement.Name}' property '{property.Definition.Name}' initial value";
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    $"{placement.Name}.Game Properties.{property.Definition.Name}",
                    VariableValueRestrictionValidator.BuildInvalidValueMessage(property.Definition.ValueRestriction, label));
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateProjectObjects(ProjectModel project)
    {
        var roots = project.GlobalScope.GameObjects
            .Concat(project.EnumerateAllObjectTemplateRoots())
            .Concat(project.BaseObjects)
            .Concat(project.EnumerateAllRoomTemplateRoots().SelectMany(static room => room.GameObjects));
        foreach (var planet in project.Planets)
        {
            roots = roots.Concat(planet.GameObjects).Concat(planet.BaseObjects).Concat(planet.ObjectTemplates)
                .Concat(planet.RoomTemplates.SelectMany(static room => room.GameObjects));
            foreach (var country in planet.Countries)
            {
                roots = roots.Concat(country.GameObjects).Concat(country.BaseObjects).Concat(country.ObjectTemplates)
                    .Concat(country.RoomTemplates.SelectMany(static room => room.GameObjects));
                foreach (var area in country.Areas)
                {
                    roots = roots.Concat(area.GameObjects).Concat(area.BaseObjects).Concat(area.ObjectTemplates)
                        .Concat(area.RoomTemplates.SelectMany(static room => room.GameObjects))
                        .Concat(area.Rooms.SelectMany(static room => room.GameObjects));
                }
            }
        }

        var visited = new HashSet<GameObject>(ReferenceEqualityComparer.Instance);
        foreach (var root in roots)
        {
            foreach (var gameObject in EnumerateObjectTree(root))
            {
                if (visited.Add(gameObject))
                {
                    yield return gameObject;
                }
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
