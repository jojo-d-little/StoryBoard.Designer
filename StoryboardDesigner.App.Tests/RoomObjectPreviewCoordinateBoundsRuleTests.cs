using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomObjectPreviewCoordinateBoundsRuleTests
{
    [Fact]
    public void Execute_WithImmediateRoomChildCoordinatesOutsideRoomBounds_ReturnsWarning()
    {
        var roomChild = new GameObject
        {
            Name = "Torch",
            PositionX = 900,
            PositionY = 601
        };

        var project = BuildProjectWithRoomObject(roomChild, width: 800, height: 600);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomObjectPreviewCoordinateBoundsRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("OBJ-006", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("outside room display bounds", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithImmediateRoomChildCoordinatesInsideRoomBounds_ReturnsNoIssues()
    {
        var roomChild = new GameObject
        {
            Name = "Table",
            PositionX = 400,
            PositionY = 300
        };

        var project = BuildProjectWithRoomObject(roomChild, width: 800, height: 600);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomObjectPreviewCoordinateBoundsRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Execute_WithNonRoomScopeObjectOutsideCanvas_ReturnsNoIssues()
    {
        var globalObject = new GameObject
        {
            Name = "GlobalMarker",
            PositionX = -10,
            PositionY = 2000
        };

        var project = new ProjectModel
        {
            Name = "BoundsScope",
            RoomImageCanvasWidth = 800,
            RoomImageCanvasHeight = 600,
            GameObjects = [globalObject]
        };

        ScopeHierarchy.AttachParents(project);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomObjectPreviewCoordinateBoundsRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
    }

    private static ProjectModel BuildProjectWithRoomObject(GameObject roomObject, int width, int height)
    {
        var room = new Room
        {
            Name = "RoomA",
            RoomImageCanvasWidth = width,
            RoomImageCanvasHeight = height,
            GameObjects = [roomObject]
        };

        var area = new Area
        {
            Name = "AreaA",
            Rooms = [room]
        };

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

        var project = new ProjectModel
        {
            Name = "BoundsValidation",
            RoomImageCanvasWidth = width,
            RoomImageCanvasHeight = height,
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);
        return project;
    }
}