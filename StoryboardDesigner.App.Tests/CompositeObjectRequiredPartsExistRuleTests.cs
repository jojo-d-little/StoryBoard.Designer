using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;

namespace StoryboardDesigner.App.Tests;

public sealed class CompositeObjectRequiredPartsExistRuleTests
{
    [Fact]
    public void ReportsWarning_WhenCompositeObjectReferencesMissingRequiredPart()
    {
        var validPart = new GameObject { Name = "Frog Hair" };
        var missingPartId = Guid.NewGuid();
        var compositeTarget = new GameObject
        {
            Name = "Potion",
            IsCompositeTarget = true,
            CompositeRequiredParts =
            [
                new CompositePartRequirement { PartObjectId = validPart.ObjectId, RequiredQuantity = 1 },
                new CompositePartRequirement { PartObjectId = missingPartId, RequiredQuantity = 2 }
            ]
        };

        var room = new Room
        {
            Name = "Lab",
            GameObjects = [validPart, compositeTarget]
        };

        var project = BuildProject(room);
        var issues = Evaluate(project);

        var issue = Assert.Single(issues, issue => issue.RuleId == "OBJ-003");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains(missingPartId.ToString("N"), issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Potion", issue.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotReport_WhenCompositeObjectRequiredPartsAllExist()
    {
        var partA = new GameObject { Name = "Frog Hair" };
        var partB = new GameObject { Name = "Tea Leaves" };
        var compositeTarget = new GameObject
        {
            Name = "Potion",
            IsCompositeTarget = true,
            CompositeRequiredParts =
            [
                new CompositePartRequirement { PartObjectId = partA.ObjectId, RequiredQuantity = 1 },
                new CompositePartRequirement { PartObjectId = partB.ObjectId, RequiredQuantity = 1 }
            ]
        };

        var room = new Room
        {
            Name = "Lab",
            GameObjects = [partA, partB, compositeTarget]
        };

        var project = BuildProject(room);
        var issues = Evaluate(project);

        Assert.DoesNotContain(issues, issue => issue.RuleId == "OBJ-003");
    }

    [Fact]
    public void DoesNotReport_WhenCompositeObjectRequiredPartExistsInCountryScopeObjects()
    {
        var countryPart = new GameObject { Name = "Country Part" };
        var compositeTarget = new GameObject
        {
            Name = "Potion",
            IsCompositeTarget = true,
            CompositeRequiredParts =
            [
                new CompositePartRequirement { PartObjectId = countryPart.ObjectId, RequiredQuantity = 1 }
            ]
        };

        var room = new Room
        {
            Name = "Lab",
            GameObjects = [compositeTarget]
        };

        var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = "Area", GameObjects = [countryPart] };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = "Country" };
        var project = new ProjectModel
        {
            Name = "CompositeValidation",
            StartingPlanetName = "Planet",
            Planets = [planet]
        };

        var issues = Evaluate(project);

        Assert.DoesNotContain(issues, issue => issue.RuleId == "OBJ-003");
    }

    private static List<ValidationIssue> Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new CompositeObjectRequiredPartsExistRule());

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
            Name = "CompositeValidation",
            StartingPlanetName = "Planet",
            Planets = [planet]
        };
    }
}