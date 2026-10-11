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

        var isWithinRoom = gameObject.EnumerateSelfAndAncestors().Any(static ancestor => ancestor is Room);

        var hasPlacementState = Math.Abs(gameObject.PositionX) > 0.0001
                                || Math.Abs(gameObject.PositionY) > 0.0001
                                || !gameObject.IncludeInPreview;

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
                "Reset PositionX/PositionY to 0 and IncludeInPreview to true, or move this object into a room hierarchy.");
            yield break;
        }

        var hasRenderableLight = gameObject.PointLight is not null;
        if (gameObject.IncludeInPreview
            && IsVisibleThroughContainment(gameObject)
            && !hasRenderableLight
            && string.IsNullOrWhiteSpace(gameObject.ResolveDefaultImagePath()))
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

    private static bool IsVisibleThroughContainment(GameObject gameObject)
    {
        var child = gameObject;
        while (child.ParentScope is GameObject parent)
        {
            var isVisibleWhenContained = child.IsInventoriable && child.ShowWhenContained.HasValue
                ? child.ShowWhenContained.Value
                : !parent.HideContainedObjects;
            if (!isVisibleWhenContained)
            {
                return false;
            }

            child = parent;
        }

        return child.ParentScope is Room;
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


