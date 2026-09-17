using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class LinkedBaseObjectReferenceExistsRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-004",
        Title: "Linked Base Object Reference Exists",
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

        var knownObjectIds = context.SharedState.GetOrAdd(
            "OBJ:KnownProjectObjectIds",
            () => BuildKnownObjectIdSet(context.Project));

        if (knownObjectIds.Contains(gameObject.LinkedBaseObjectId.Value))
        {
            yield break;
        }

        var objectName = string.IsNullOrWhiteSpace(gameObject.ScopeName) ? "(unnamed object)" : gameObject.ScopeName.Trim();
        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            BuildScopePath(gameObject),
            $"object '{objectName}' references linked base object id '{gameObject.LinkedBaseObjectId.Value:N}' that does not exist in the project.");
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

    private static HashSet<Guid> BuildKnownObjectIdSet(ProjectModel project)
    {
        var known = new HashSet<Guid>();

        static void AddObject(GameObject obj, ISet<Guid> set)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                set.Add(obj.ObjectId);
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddObject(child, set);
            }
        }

        foreach (var template in project.ObjectTemplates)
        {
            AddObject(template, known);
        }

        foreach (var roomTemplateObject in project.RoomTemplates.SelectMany(room => room.GameObjects))
        {
            AddObject(roomTemplateObject, known);
        }

        foreach (var baseObject in project.BaseObjects)
        {
            AddObject(baseObject, known);
        }

        foreach (var playerObject in project.GlobalScope.GameObjects)
        {
            AddObject(playerObject, known);
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
            AddObject(scopedObject, known);
        }

        foreach (var roomObject in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms)
                     .SelectMany(room => room.GameObjects))
        {
            AddObject(roomObject, known);
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
            AddObject(scopedBaseObject, known);
        }

        return known;
    }
}

