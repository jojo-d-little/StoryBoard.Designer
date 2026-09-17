using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class AdjacentRoomDuplicateNormalizedNameWarningRuleTests
{
    [Fact]
    public void Evaluate_DuplicateNormalizedRoomNamesInArea_ProducesWarning()
    {
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
                                    Rooms =
                                    [
                                        new Room { Name = "East Wing" },
                                        new Room { Name = "east   wing" }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new AdjacentRoomDuplicateNormalizedNameWarningRule());

        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("NAV-001", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("Duplicate normalized room name", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DistinctNormalizedRoomNamesInArea_DoesNotProduceWarning()
    {
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
                                    Rooms =
                                    [
                                        new Room { Name = "East Wing" },
                                        new Room { Name = "East Workshop" }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new AdjacentRoomDuplicateNormalizedNameWarningRule());

        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Empty(issues);
    }
}
