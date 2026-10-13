using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class ObjectImageTransparencyRangeRule : IValidationRule
{
    private static readonly IReadOnlySet<ScopeNodeKind> SupportedKinds =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds => SupportedKinds;

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJECT-IMAGE-TRANSPARENCY-001",
        Title: "Object Image Transparency Range",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not GameObject gameObject)
        {
            yield break;
        }

        var value = gameObject.ImageTransparency;
        if (double.IsFinite(value) && value is >= 0 and <= 1)
        {
            yield break;
        }

        var path = string.Join(" / ", gameObject.EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
        if (string.IsNullOrWhiteSpace(path))
        {
            path = gameObject.Name;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            $"{path} / ImageTransparency",
            "Image transparency must be a finite number from 0 to 1.",
            "Set image transparency to 0 (visible), a value between 0 and 1, or 1 (fully transparent).");
    }
}
