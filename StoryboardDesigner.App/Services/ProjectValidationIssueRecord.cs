namespace StoryboardDesigner.App.Services;

public sealed record ProjectValidationIssue(
    ValidationSeverity Severity,
    string Path,
    string Description,
    string? Hint = null,
    string RuleId = "");

