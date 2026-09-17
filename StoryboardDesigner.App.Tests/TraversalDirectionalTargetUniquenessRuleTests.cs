using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalDirectionalTargetUniquenessRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenRoomHasCompetingTargetsForSameDirection()
    {
        var source = new Room { Name = "Ground" };
        var upperA = new Room { Name = "UpperA" };
        var upperB = new Room { Name = "UpperB" };

        var area = new Area
        {
            Name = "Tower",
            Rooms = [source, upperA, upperB],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = source.Id,
                    RoomBId = upperA.Id,
                    BaseTraversalDirectionFromA = Direction10.Up,
                    TraversalAccessMode = TraversalAccessMode.OneWayAtoB
                },
                new TraversalConnection
                {
                    RoomAId = source.Id,
                    RoomBId = upperB.Id,
                    BaseTraversalDirectionFromA = Direction10.Up,
                    TraversalAccessMode = TraversalAccessMode.OneWayAtoB
                }
            ]
        };

        var project = BuildProject(area);
        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-007", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("multiple traversal targets", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("direction Up", issue.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_WhenDirectionsDiffer()
    {
        var source = new Room { Name = "Ground" };
        var upper = new Room { Name = "Upper" };
        var lower = new Room { Name = "Lower" };

        var area = new Area
        {
            Name = "Tower",
            Rooms = [source, upper, lower],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = source.Id,
                    RoomBId = upper.Id,
                    BaseTraversalDirectionFromA = Direction10.Up,
                    TraversalAccessMode = TraversalAccessMode.OneWayAtoB
                },
                new TraversalConnection
                {
                    RoomAId = source.Id,
                    RoomBId = lower.Id,
                    BaseTraversalDirectionFromA = Direction10.Down,
                    TraversalAccessMode = TraversalAccessMode.OneWayAtoB
                }
            ]
        };

        var project = BuildProject(area);
        var issues = ExecuteRule(project);

        Assert.Empty(issues);
    }

    private static IReadOnlyList<ValidationIssue> ExecuteRule(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new TraversalDirectionalTargetUniquenessRule());

        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues;
    }

    private static ProjectModel BuildProject(Area area)
    {
        var country = new Country
        {
            Name = "CountryA",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "PlanetA",
            Countries = [country]
        };

        return new ProjectModel
        {
            Name = "TraversalDirectionalUniqueness",
            Planets = [planet]
        };
    }
}
