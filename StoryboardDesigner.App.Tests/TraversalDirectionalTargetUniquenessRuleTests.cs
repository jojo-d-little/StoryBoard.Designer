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
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("multiple traversal targets", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("direction Up", issue.Description, StringComparison.Ordinal);
        Assert.Contains("destination-qualified", issue.Hint, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void Evaluate_ThreeSouthDestinations_AdvisesQualificationWithoutBlockingAuthoring()
    {
        var large = new Room { Name = "Large" };
        var neighbors = Enumerable.Range(1, 3).Select(index => new Room { Name = $"South {index}" }).ToArray();
        var area = new Area
        {
            Name = "Area",
            Rooms = [large, ..neighbors],
            TraversalConnections = neighbors.Select(room => new TraversalConnection
            {
                RoomAId = large.Id,
                RoomBId = room.Id,
                BaseTraversalDirectionFromA = Direction10.South,
                TraversalAccessMode = TraversalAccessMode.TwoWay
            }).ToList()
        };

        var issues = ExecuteRule(BuildProject(area));

        var issue = Assert.Single(issues);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("South 1", issue.Description);
        Assert.Contains("South 2", issue.Description);
        Assert.Contains("South 3", issue.Description);
        Assert.Contains("destination-qualified", issue.Hint);
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
