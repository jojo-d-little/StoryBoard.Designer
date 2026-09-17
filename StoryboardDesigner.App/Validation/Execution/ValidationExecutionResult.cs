using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed record ValidationExecutionResult(
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<string> ExecutedRuleIds,
    IReadOnlyList<ValidationSkippedRuleInfo> SkippedRules,
    ValidationExecutionKind ExecutionKind,
    string RootScopePath,
    bool IncludeDescendants,
    int CandidateNodeCount,
    bool StoppedEarly,
    int RemainingIssueCountAtStop,
    string? StopRuleId,
    int RemainingRuleCountAtStop);
