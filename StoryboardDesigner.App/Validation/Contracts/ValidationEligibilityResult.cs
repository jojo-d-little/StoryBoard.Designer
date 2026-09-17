namespace StoryboardDesigner.App.Validation.Contracts;

public sealed record ValidationEligibilityResult(
    bool IsEligible,
    ValidationSkipReason? SkipReason = null,
    string? SkipDetail = null)
{
    public static ValidationEligibilityResult Eligible() => new(true);
}
