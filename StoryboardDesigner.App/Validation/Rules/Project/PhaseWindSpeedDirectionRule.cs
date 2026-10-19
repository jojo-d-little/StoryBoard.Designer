using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class PhaseWindSpeedDirectionRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-020",
        Title: "Phase Wind Speed Direction",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Phases");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not ProjectModel project)
        {
            yield break;
        }

        foreach (var phase in project.PhaseBooks)
        {
            foreach (var issue in Evaluate(phase, inheritedDirection: null, inheritedSpeed: null, "Global / Phases"))
            {
                yield return issue;
            }
        }
    }

    private IEnumerable<ValidationIssue> Evaluate(
        PhaseNode phase,
        int? inheritedDirection,
        double? inheritedSpeed,
        string parentPath)
    {
        var direction = phase.WindDirection ?? inheritedDirection;
        var speed = phase.WindSpeed ?? inheritedSpeed;
        var label = string.IsNullOrWhiteSpace(phase.DisplayName) ? phase.PhaseKey : phase.DisplayName;
        var path = $"{parentPath} / {phase.Tier}:{label}";

        if (speed is > 0 && double.IsFinite(speed.Value) && direction is null)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                path,
                "Effective wind speed is positive, but no wind direction is set on this phase or an ancestor.",
                "Set a direction from 0 to 359, or set wind speed to 0 to keep the phase calm.");
        }

        foreach (var child in phase.Children)
        {
            foreach (var issue in Evaluate(child, direction, speed, path))
            {
                yield return issue;
            }
        }
    }
}
