namespace StoryboardDesigner.App.Validation.Contracts;

public sealed record ValidationRuleMetadata(
    string RuleId,
    string Title,
    ValidationSeverity DefaultSeverity,
    string Category);
