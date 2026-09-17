using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class LinkedBaseObjectSingleLevelRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-007",
        Title: "Linked Base Object Must Be Single-Level",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not GameObject gameObject
            || !gameObject.LinkedBaseObjectId.HasValue
            || gameObject.LinkedBaseObjectId.Value == Guid.Empty)
        {
            yield break;
        }

        var objectLookup = context.SharedState.GetOrAdd(
            "OBJ:ProjectObjectsById",
            () => BuildObjectLookup(context.Project));

        if (!objectLookup.TryGetValue(gameObject.LinkedBaseObjectId.Value, out var linkedBase))
        {
            yield break;
        }

        if (!linkedBase.LinkedBaseObjectId.HasValue || linkedBase.LinkedBaseObjectId.Value == Guid.Empty)
        {
            yield break;
        }

        var objectName = string.IsNullOrWhiteSpace(gameObject.ScopeName) ? "(unnamed object)" : gameObject.ScopeName.Trim();
        var linkedBaseName = string.IsNullOrWhiteSpace(linkedBase.ScopeName) ? "(unnamed object)" : linkedBase.ScopeName.Trim();

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            BuildScopePath(gameObject),
            $"object '{objectName}' links to '{linkedBaseName}' ({linkedBase.ObjectId:N}), but that target is itself linked. Only single-level base-to-instance links are allowed.");
    }

    private static string BuildScopePath(GameObject gameObject)
    {
        var scopes = gameObject
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(node => node.ScopeName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return scopes.Count == 0
            ? "Project"
            : string.Join(" / ", scopes);
    }

    private static Dictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
    {
        var lookup = new Dictionary<Guid, GameObject>();

        static void AddObject(GameObject obj, IDictionary<Guid, GameObject> target)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                target[obj.ObjectId] = obj;
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddObject(child, target);
            }
        }

        foreach (var template in project.ObjectTemplates)
        {
            AddObject(template, lookup);
        }

        foreach (var roomTemplateObject in project.RoomTemplates.SelectMany(room => room.GameObjects))
        {
            AddObject(roomTemplateObject, lookup);
        }

        foreach (var baseObject in project.BaseObjects)
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
            AddObject(scopedBaseObject, lookup);
        }

        return lookup;
    }
}


