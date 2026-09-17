using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class DuplicateNameInScopeRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_ForDuplicatePlanetNames()
    {
        var project = new ProjectModel
        {
            Planets =
            [
                new Planet { Name = "Earth" },
                new Planet { Name = "earth" }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new DuplicateNameInScopeRule());

        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("NAME-001", issue.RuleId);
        Assert.Equal("Global", issue.Path);
        Assert.Contains("Duplicate planet name", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_IgnoresQuantifiableIndividualInstanceDuplicatesInRoom()
    {
        var room = new Room
        {
            Name = "Room A",
            GameObjects =
            [
                new GameObject
                {
                    Name = "Coin",
                    IsQuantifiable = true,
                    QuantifiablePlacementDistributionMode = "IndividualInstances"
                },
                new GameObject
                {
                    Name = "Coin",
                    IsQuantifiable = true,
                    QuantifiablePlacementDistributionMode = "IndividualInstances"
                }
            ]
        };

        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Planet A",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country A",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area A",
                                    Rooms = [room]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new DuplicateNameInScopeRule());

        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Empty(issues);
    }
}
