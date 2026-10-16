using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class RoomObjectPreviewPlacementRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-005",
        Title: "Room Object Preview Placement Consistency",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not GameObject gameObject)
        {
            yield break;
        }

        var objectLookup = BuildObjectLookup(context.Project);
        GameObject Effective(GameObject candidate) => candidate.ResolveEffectiveDefinitionOwnedSource(id => objectLookup.GetValueOrDefault(id));

        var isWithinRoom = gameObject.EnumerateSelfAndAncestors().Any(static ancestor => ancestor is Room);

        var hasPlacementState = Math.Abs(gameObject.PositionX) > 0.0001
                                || Math.Abs(gameObject.PositionY) > 0.0001;

        if (!isWithinRoom)
        {
            if (!hasPlacementState)
            {
                yield break;
            }

            var objectName = string.IsNullOrWhiteSpace(gameObject.ScopeName) ? "(unnamed object)" : gameObject.ScopeName.Trim();
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                BuildScopePath(gameObject),
                $"object '{objectName}' uses room preview placement fields outside a room object hierarchy.",
                "Reset PositionX/PositionY to 0, or move this object into a room hierarchy.");
            yield break;
        }

        var effectiveSettings = Effective(gameObject);
        var hasRenderableLight = effectiveSettings.PointLight is not null;
        var hasSoundEmitter = effectiveSettings.SoundEmitters.Count > 0;
        if (effectiveSettings.IncludeInPreview
            && IsVisibleThroughContainment(gameObject, Effective)
            && !hasRenderableLight
            && !hasSoundEmitter
            && string.IsNullOrWhiteSpace(effectiveSettings.ResolveDefaultImagePath()))
        {
            var objectName = string.IsNullOrWhiteSpace(gameObject.ScopeName) ? "(unnamed object)" : gameObject.ScopeName.Trim();
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                BuildScopePath(gameObject),
            $"object '{objectName}' is included in room preview but has no configured default image path.",
            "Assign a default image variant path or uncheck Include In Preview.");
        }
    }

    private static bool IsVisibleThroughContainment(GameObject gameObject, Func<GameObject, GameObject> effective)
    {
        var child = gameObject;
        while (child.ParentScope is GameObject parent)
        {
            var effectiveChild = effective(child);
            var effectiveParent = effective(parent);
            var isVisibleWhenContained = effectiveChild.IsInventoriable && effectiveChild.ShowWhenContained.HasValue
                ? effectiveChild.ShowWhenContained.Value
                : !effectiveParent.HideContainedObjects;
            if (!isVisibleWhenContained)
            {
                return false;
            }

            child = parent;
        }

        return child.ParentScope is Room;
    }

    private static Dictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
    {
        var lookup = new Dictionary<Guid, GameObject>();

        void Add(GameObject obj)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                lookup[obj.ObjectId] = obj;
            }

            foreach (var child in obj.ContainedObjects)
            {
                Add(child);
            }
        }

        foreach (var obj in project.EnumerateAllObjectTemplateRoots()
                     .Concat(project.EnumerateAllRoomTemplateRoots().SelectMany(static room => room.GameObjects))
                     .Concat(project.BaseObjects)
                     .Concat(project.GlobalScope.GameObjects)
                     .Concat(project.Planets.SelectMany(static planet => planet.GameObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.BaseObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.GameObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.BaseObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.GameObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.BaseObjects))
                     .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.Rooms).SelectMany(static room => room.GameObjects)))
        {
            Add(obj);
        }

        return lookup;
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
}


