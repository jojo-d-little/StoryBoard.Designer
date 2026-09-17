namespace StoryboardDesigner.App.Validation.Contracts;

public sealed record ValidationIssue(
    string RuleId,
    ValidationSeverity Severity,
    string Path,
    string Description,
    string? Hint = null);
