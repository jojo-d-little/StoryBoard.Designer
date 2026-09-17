using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class RoomObjectPreviewCoordinateBoundsRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-006",
        Title: "Room Object Preview Coordinates Within Room Bounds",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not GameObject gameObject
            || gameObject.ParentScope is not Room)
        {
            yield break;
        }

        var room = (Room)gameObject.ParentScope;
        var widthBound = room.RoomImageCanvasWidth > 0
            ? room.RoomImageCanvasWidth
            : Math.Max(0, context.Project.RoomImageCanvasWidth);
        var heightBound = room.RoomImageCanvasHeight > 0
            ? room.RoomImageCanvasHeight
            : Math.Max(0, context.Project.RoomImageCanvasHeight);

        var x = double.IsFinite(gameObject.PositionX) ? gameObject.PositionX : 0;
        var y = double.IsFinite(gameObject.PositionY) ? gameObject.PositionY : 0;

        if (x >= 0 && x <= widthBound && y >= 0 && y <= heightBound)
        {
            yield break;
        }

        var objectName = string.IsNullOrWhiteSpace(gameObject.ScopeName) ? "(unnamed object)" : gameObject.ScopeName.Trim();
        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            BuildScopePath(gameObject),
            $"object '{objectName}' has room preview coordinates ({x:0.###}, {y:0.###}) outside room display bounds 0..{widthBound} x 0..{heightBound}.",
            "Move the object into bounds or adjust the room width/height settings.");
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

