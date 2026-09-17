using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;

namespace StoryboardDesigner.App.Tests;

internal static class ValidationRuleTestContext
{
    internal static ValidationRuleContext Create(ProjectModel project)
    {
        var request = new ValidationExecutionRequest(project);
        var root = project;
        return new ValidationRuleContext(
            project,
            root,
            root,
            request,
            ValidationProfile.Unspecified,
            IncludeDescendants: true,
            IsRecursivePass: true,
            IsFullProject: true,
            ValidationLookupService.Build(project),
            new ValidationSharedState());
    }
}
