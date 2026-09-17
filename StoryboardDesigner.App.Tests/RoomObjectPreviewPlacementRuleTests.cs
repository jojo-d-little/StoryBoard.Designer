using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomObjectPreviewPlacementRuleTests
{
    [Fact]
    public void Execute_WithImmediateRoomChildIncludedWithoutImage_ReturnsWarning()
    {
        var roomChild = new GameObject
        {
            Name = "Lamp",
            IncludeInPreview = true
        };

        var project = BuildProjectWithRoomObject(roomChild);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomObjectPreviewPlacementRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("OBJ-005", issue.RuleId);
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("included in room preview", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithImmediateRoomChildIncludedWithDefaultVariant_DoesNotReturnWarning()
    {
        var roomChild = new GameObject
        {
            Name = "Lamp",
            IncludeInPreview = true,
            ImageVariants =
            [
                new ObjectImageVariant
                {
                    VariantName = "default",
                    FullImagePath = "lamp.png",
                    IsDefault = true
                }
            ]
        };

        var project = BuildProjectWithRoomObject(roomChild);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomObjectPreviewPlacementRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Execute_WithNestedRoomDescendantPlacementState_ReturnsWarning()
    {
        var nestedChild = new GameObject
        {
            Name = "Coin",
            PositionX = 12,
            PositionY = 8,
            IncludeInPreview = true
        };

        var parent = new GameObject
        {
            Name = "Chest",
            ImageVariants =
            [
                new ObjectImageVariant
                {
                    VariantName = "default",
                    FullImagePath = "chest.png",
                    IsDefault = true
                }
            ],
            ContainedObjects = [nestedChild]
        };

        var project = BuildProjectWithRoomObject(parent);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomObjectPreviewPlacementRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(
            result.Issues,
            static item => item.Description.Contains("nested room descendant", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("OBJ-005", issue.RuleId);
    }

    [Fact]
    public void Execute_WithNonRoomScopePlacementState_ReturnsWarning()
    {
        var globalObject = new GameObject
        {
            Name = "GlobalFlag",
            PositionX = 22,
            IncludeInPreview = false
        };

        var project = new ProjectModel
        {
            Name = "PlacementScope",
            GameObjects = [globalObject]
        };

        ScopeHierarchy.AttachParents(project);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RoomObjectPreviewPlacementRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("OBJ-005", issue.RuleId);
        Assert.Contains("non-room scope", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectModel BuildProjectWithRoomObject(GameObject roomObject)
    {
        var room = new Room
        {
            Name = "RoomA",
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
            Name = "PlacementValidation",
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);
        return project;
    }
}
