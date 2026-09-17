using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomPlacementReferenceIntegrityRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenPlacementReferencesRoomOutsideArea()
    {
        var existingRoom = new Room { Name = "Room A" };

        var area = new Area
        {
            Name = "Area",
            Rooms = [existingRoom],
            RoomPlacements =
            [
                new AreaRoomPlacement
                {
                    RoomId = Guid.NewGuid(),
                    X = 10,
                    Y = 20
                }
            ]
        };

        var project = BuildProject(area);
        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomPlacementReferenceIntegrityRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("TRV-006", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("not part of this area", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsIssue_WhenDuplicatePlacementExistsForSameRoom()
    {
        var room = new Room { Name = "Room A" };

        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = room.Id, X = 10, Y = 20 },
                new AreaRoomPlacement { RoomId = room.Id, X = 30, Y = 40 }
            ]
        };

        var project = BuildProject(area);
        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomPlacementReferenceIntegrityRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("TRV-006", issue.RuleId);
        Assert.Contains("duplicate room placement", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenPlacementsAreUniqueAndInArea()
    {
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };

        var area = new Area
        {
            Name = "Area",
            Rooms = [roomA, roomB],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = roomA.Id, X = 10, Y = 20 },
                new AreaRoomPlacement { RoomId = roomB.Id, X = 30, Y = 40 }
            ]
        };

        var project = BuildProject(area);
        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomPlacementReferenceIntegrityRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
        Assert.Contains("TRV-006", result.ExecutedRuleIds);
    }

    private static ProjectModel BuildProject(Area area)
    {
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };

        return new ProjectModel
        {
            Name = "RoomPlacementValidationProject",
            Planets = [planet]
        };
    }
}