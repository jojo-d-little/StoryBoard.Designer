using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Contracts;

public interface IValidationRule
{
    ValidationRuleMetadata Metadata { get; }
    IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; }
    bool SupportsActionCandidates => false;
    ValidationEligibilityResult CanEvaluate(ValidationEligibilityContext eligibility) => ValidationEligibilityResult.Eligible();
    IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context);
}
