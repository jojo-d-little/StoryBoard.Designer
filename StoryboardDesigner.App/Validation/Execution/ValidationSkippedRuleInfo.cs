using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed record ValidationSkippedRuleInfo(
    string RuleId,
    ValidationSkipReason SkipReason,
    string? SkipDetail = null);
