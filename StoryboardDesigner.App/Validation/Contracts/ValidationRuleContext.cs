using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;

namespace StoryboardDesigner.App.Validation.Contracts;

public sealed record ValidationRuleContext(
    ProjectModel Project,
    ScopeNodeBase CandidateNode,
    ScopeNodeBase RequestRoot,
    ValidationExecutionRequest Request,
    ValidationProfile ActiveProfile,
    bool IncludeDescendants,
    bool IsRecursivePass,
    bool IsFullProject,
    IValidationLookupService Lookup,
    IValidationSharedState SharedState,
    CancellationToken CancellationToken = default);
