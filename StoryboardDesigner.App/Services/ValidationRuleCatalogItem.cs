using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Services;

public sealed record ValidationRuleCatalogItem(
    string RuleId,
    string Title,
    string Category,
    ValidationSeverity DefaultSeverity);
