using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed record ValidationIssueRunProcessingResult(
    IReadOnlyList<ValidationIssue> Issues,
    bool StoppedEarly,
    int RemainingIssueCountAtStop,
    string? StopRuleId);
