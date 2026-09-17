using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;

namespace StoryboardDesigner.App.Tests;

public sealed class LinkedBaseObjectSingleLevelRuleTests
{
    [Fact]
    public void ReportsError_WhenLinkedObjectTargetsAnotherLinkedObject()
    {
        var baseObject = new GameObject { Name = "Base Torch" };
        var linkedIntermediate = new GameObject
        {
            Name = "Linked Intermediate",
            LinkedBaseObjectId = baseObject.ObjectId,
            LinkActionsToBaseObject = true
        };
        var linkedLeaf = new GameObject
        {
            Name = "Linked Leaf",
            LinkedBaseObjectId = linkedIntermediate.ObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Cave",
            GameObjects = [baseObject, linkedIntermediate, linkedLeaf]
        };

        var project = BuildProject(room);
        var issues = Evaluate(project);

        var issue = Assert.Single(issues, item => item.RuleId == "OBJ-007");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("Linked Leaf", issue.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotReport_WhenLinkedObjectTargetsConcreteBase()
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

        Assert.DoesNotContain(issues, issue => issue.RuleId == "OBJ-007");
    }

    private static List<ValidationIssue> Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new LinkedBaseObjectSingleLevelRule());

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
