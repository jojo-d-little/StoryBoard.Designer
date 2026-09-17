using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class ScopedActionNameUniquenessRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenScopeContainsDuplicateActionNames_IgnoringCase()
    {
        var room = new Room
        {
            Name = "Room A",
            AvailableActions =
            [
                new CommandAction { Name = "Inspect" },
                new CommandAction { Name = " inspect " }
            ]
        };

        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "Scoped Action Rule", Planets = [planet] };

        var result = Evaluate(project);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-016", issue.RuleId);
        Assert.Contains("Duplicate action name 'Inspect'", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Room A", issue.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PROJ-016", result.ExecutedRuleIds);
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_WhenMatchingActionNamesExistInDifferentScopes()
    {
        var room = new Room
        {
            Name = "Room A",
            AvailableActions =
            [
                new CommandAction { Name = "Inspect" }
            ]
        };

        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "Scoped Action Rule",
            Planets = [planet],
            GlobalObjectAvailableActions =
            [
                new CommandAction { Name = "Inspect" }
            ]
        };

        var result = Evaluate(project);

        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "PROJ-016");
        Assert.Contains("PROJ-016", result.ExecutedRuleIds);
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new ScopedActionNameUniquenessRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }
}