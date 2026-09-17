using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;

namespace StoryboardDesigner.App.Validation.Contracts;

public sealed record ValidationEligibilityContext(
    ValidationExecutionRequest Request,
    ScopeNodeBase CandidateNode,
    ScopeNodeBase RequestRoot,
    bool IsRecursivePass,
    bool IsFullProject,
    ValidationProfile ActiveProfile,
    IReadOnlySet<string> IncludedRuleIds,
    IReadOnlySet<string> ExcludedRuleIds,
    ValidationNodeFacetSet CandidateFacets);
