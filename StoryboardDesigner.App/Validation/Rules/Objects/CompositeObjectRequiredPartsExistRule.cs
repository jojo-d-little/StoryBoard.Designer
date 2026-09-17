using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class CompositeObjectRequiredPartsExistRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-003",
        Title: "Composite Object Required Parts Exist",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not GameObject gameObject || !gameObject.IsCompositeTarget)
        {
            yield break;
        }

        var requiredPartIds = (gameObject.CompositeRequiredParts ?? new List<CompositePartRequirement>())
            .Select(part => part.PartObjectId)
            .Where(id => id != Guid.Empty)
            .ToList();
        if (requiredPartIds.Count == 0)
        {
            yield break;
        }

        var knownObjectIds = context.SharedState.GetOrAdd(
            "OBJ:KnownProjectObjectIds",
            () => BuildKnownObjectIdSet(context.Project));

        var missingPartIds = requiredPartIds
            .Where(id => !knownObjectIds.Contains(id))
            .Distinct()
            .ToList();
        if (missingPartIds.Count == 0)
        {
            yield break;
        }

        var objectName = string.IsNullOrWhiteSpace(gameObject.ScopeName) ? "(unnamed object)" : gameObject.ScopeName.Trim();
        var missingList = string.Join(", ", missingPartIds.Select(id => id.ToString("N")));

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            BuildScopePath(gameObject),
            $"composite object '{objectName}' references required part id(s) that do not exist in the project: {missingList}.");
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

