using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomCanvasDimensionsRuleTests
{
    [Fact]
    public void Evaluate_WhenRoomCanvasIsValid_ReturnsNoIssues()
    {
        var room = new Room
        {
            Name = "Hallway",
            RoomImageCanvasWidth = 600,
            RoomImageCanvasHeight = 800
        };

        var project = BuildProject(room, projectCellSize: 40);
        var issues = Execute(project);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_WhenRoomCanvasNotDivisibleByProjectCellSize_ReturnsError()
    {
        var room = new Room
        {
            Name = "Hallway",
            RoomImageCanvasWidth = 610,
            RoomImageCanvasHeight = 800
        };

        var project = BuildProject(room, projectCellSize: 40);
        var issue = Assert.Single(Execute(project));

        Assert.Equal("ROOM-001", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("not divisible", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenDerivedGridCellCountsOutsideRange_ReturnsError()
    {
        var room = new Room
        {
            Name = "Tiny",
            RoomImageCanvasWidth = 80,
            RoomImageCanvasHeight = 80
        };

        var project = BuildProject(room, projectCellSize: 40);
        var issue = Assert.Single(Execute(project));

        Assert.Equal("ROOM-001", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("outside allowed range", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ValidationIssue> Execute(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomCanvasDimensionsRule());

        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }

    private static ProjectModel BuildProject(Room room, int projectCellSize)
    {
        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Country",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Planet",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "RoomCanvasValidation",
            RoomDesignerGridCellSize = projectCellSize,
            StartingPlanetName = planet.Name,
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);
        return project;
    }
}
