using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class InvalidStartingScopeRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenProjectHasNoPlanets()
    {
        var project = new ProjectModel { Name = "No Planets" };
        var registry = new ValidationRuleRegistry();
        registry.Register(new InvalidStartingScopeRule());

        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("PROJ-002", issue.RuleId);
        Assert.Equal("Project", issue.Path);
        Assert.Contains("no planets", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsIssue_WhenAreaStartingRoomIdIsInvalid()
    {
        var room = new Room { Name = "Room A" };
        var area = new Area
        {
            Name = "Area A",
            Rooms = [room],
            StartingRoomId = Guid.NewGuid()
        };

        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets =
            [
                new Planet
                {
                    Name = "Planet A",
                    StartingCountryName = "Country A",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country A",
                            StartingAreaName = "Area A",
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new InvalidStartingScopeRule());

        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("Global / Planet A / Country A / Area A", issue.Path);
        Assert.Contains("invalid starting room id", issue.Description, StringComparison.OrdinalIgnoreCase);
    }
}
