using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

/// <summary>Validates the one-level composition and required placement data for structural parts.</summary>
public sealed class StructuralPartCompositionRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-021",
        Title: "Structural Parts Must Be One Level Deep",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not GameObject gameObject || !gameObject.IsPartOfParent)
        {
            yield break;
        }

        var parent = gameObject.ParentScope as GameObject;
        if (parent?.IsPartOfParent == true)
        {
            yield return Issue(gameObject,
                $"Structural part '{gameObject.Name}' is a child of structural part '{parent.Name}'. Structural parts may not contain another marked structural part.");
        }

        if (gameObject.Placement.Mode == StructuralPartPlacementKind.ParentRelative
            && (!IsFinite(gameObject.Placement.ParentRelativeOffsetX)
                || !IsFinite(gameObject.Placement.ParentRelativeOffsetY)))
        {
            yield return Issue(gameObject,
                $"Structural part '{gameObject.Name}' needs finite parent-relative X and Y offsets.");
        }
        else if (gameObject.Placement.Mode == StructuralPartPlacementKind.ParentRelative
            && (!IsGridAligned(gameObject.Placement.ParentRelativeOffsetX, context.Project.RoomDesignerGridCellSize)
                || !IsGridAligned(gameObject.Placement.ParentRelativeOffsetY, context.Project.RoomDesignerGridCellSize)))
        {
            yield return Issue(gameObject,
                $"Structural part '{gameObject.Name}' needs grid-aligned parent-relative offsets.");
        }

        if (gameObject.Placement.Mode == StructuralPartPlacementKind.RoomCoordinates
            && (!IsFinite(gameObject.Placement.RoomCoordinateX) || !IsFinite(gameObject.Placement.RoomCoordinateY)))
        {
            yield return Issue(gameObject,
                $"Structural part '{gameObject.Name}' needs finite room X and Y coordinates.");
        }
    }

    private ValidationIssue Issue(GameObject gameObject, string message)
    {
        var path = gameObject.EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name));
        return new ValidationIssue(Metadata.RuleId, Metadata.DefaultSeverity, string.Join(" / ", path), message);
    }

    private static bool IsFinite(double? value) => value.HasValue && double.IsFinite(value.Value);

    private static bool IsGridAligned(double? value, int gridSize)
    {
        if (!IsFinite(value) || gridSize <= 0)
        {
            return false;
        }

        var cellCount = value!.Value / gridSize;
        return Math.Abs(cellCount - Math.Round(cellCount)) <= 1e-9;
    }
}
