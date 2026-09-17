using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;

namespace StoryboardDesigner.App.Tests;

public sealed class LinkedBaseObjectReferenceExistsRuleTests
{
    [Fact]
    public void ReportsError_WhenLinkedBaseObjectReferenceMissing()
    {
        var linkedObject = new GameObject
        {
            Name = "Linked Torch",
            LinkedBaseObjectId = Guid.NewGuid(),
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Cave",
            GameObjects = [linkedObject]
        };

        var project = BuildProject(room);
        var issues = Evaluate(project);

        var issue = Assert.Single(issues, issue => issue.RuleId == "OBJ-004");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("Linked Torch", issue.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotReport_WhenLinkedBaseObjectReferenceExists()
    {
        var baseObject = new GameObject { Name = "Base Torch" };
        var linkedObject = new GameObject
        {
            Name = "Linked Torch",
            LinkedBaseObjectId = baseObject.ObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Cave",
            GameObjects = [baseObject, linkedObject]
        };

        var project = BuildProject(room);
        var issues = Evaluate(project);

        Assert.DoesNotContain(issues, issue => issue.RuleId == "OBJ-004");
    }

    [Fact]
    public void DoesNotReport_WhenLinkedBaseObjectReferenceExistsInPlanetScopeObjects()
    {
        var planetScopedBase = new GameObject { Name = "Planet Torch" };
        var linkedObject = new GameObject
        {
            Name = "Linked Torch",
            LinkedBaseObjectId = planetScopedBase.ObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Cave",
            GameObjects = [linkedObject]
        };

        var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = "Area" };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = "Country", GameObjects = [planetScopedBase] };
        var project = new ProjectModel
        {
            Name = "LinkedBaseValidation",
            StartingPlanetName = "Planet",
            Planets = [planet]
        };

        var issues = Evaluate(project);

        Assert.DoesNotContain(issues, issue => issue.RuleId == "OBJ-004");
    }

    private static List<ValidationIssue> Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new LinkedBaseObjectReferenceExistsRule());

        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }

    private static ProjectModel BuildProject(Room room)
    {
        var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = "Area" };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = "Country" };

        return new ProjectModel
        {
            Name = "LinkedBaseValidation",
            StartingPlanetName = "Planet",
            Planets = [planet]
        };
    }
}