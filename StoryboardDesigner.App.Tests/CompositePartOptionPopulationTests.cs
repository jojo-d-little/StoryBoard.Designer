using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class CompositePartOptionPopulationTests
{
    [Fact]
    public void EditObjectBasicProperties_RoomObject_IncludesDistinctInventoriableParts_WhenCompositeRecipeIdsMatch()
    {
        var sharedRecipeId = Guid.NewGuid();

        var target = new GameObject
        {
            Name = "Workbench"
        };

        var partA = new GameObject
        {
            Name = "Metal Rod",
            IsInventoriable = true,
            CompositeRecipeId = sharedRecipeId
        };

        var partB = new GameObject
        {
            Name = "Metal Plate",
            IsInventoriable = true,
            CompositeRecipeId = sharedRecipeId
        };

        var room = new Room
        {
            Name = "Garage",
            GameObjects = [target, partA, partB]
        };

        var area = new Area
        {
            Name = "Industrial",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Factory",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Earth",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "Composite Parts",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(target, objectsNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.False(edited);
        var request = Assert.IsType<ObjectBasicPropertiesEditRequest>(treeContext.LastObjectBasicPropertiesRequest);

        Assert.Contains(request.AvailableCompositePartOptions, option => option.Id == partA.ObjectId && option.Name == partA.Name);
        Assert.Contains(request.AvailableCompositePartOptions, option => option.Id == partB.ObjectId && option.Name == partB.Name);
        Assert.DoesNotContain(request.AvailableCompositePartOptions, option => option.Id == target.ObjectId);
    }

    [Fact]
    public void GetCompositeRecipeChoicesForScopeNode_UsesAuthoredObjectIds_ForTargetAndRequiredParts()
    {
        var froghair = new GameObject
        {
            Name = "Froghair",
            IsInventoriable = true
        };

        var teaLeaves = new GameObject
        {
            Name = "TeaLeaves",
            IsInventoriable = true
        };

        var smokeBall = new GameObject
        {
            Name = "SmokeBall",
            IsCompositeTarget = true,
            CompositeRecipeId = Guid.NewGuid(),
            CompositeRequiredParts =
            [
                new CompositePartRequirement { PartObjectId = froghair.ObjectId, RequiredQuantity = 1 },
                new CompositePartRequirement { PartObjectId = teaLeaves.ObjectId, RequiredQuantity = 1 }
            ]
        };

        var room = new Room
        {
            Name = "New Room",
            GameObjects = [froghair, teaLeaves, smokeBall]
        };

        var area = new Area
        {
            Name = "New Area",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "New Country",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "New Planet",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "Composite IDs",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var choices = viewModel.GetCompositeRecipeChoicesForScopeNode(room);
        var choice = Assert.Single(choices);

        Assert.Equal(smokeBall.ObjectId, choice.TargetObjectId);
        Assert.Contains(froghair.ObjectId, choice.RequiredPartObjectIds);
        Assert.Contains(teaLeaves.ObjectId, choice.RequiredPartObjectIds);
    }

    [Fact]
    public void EditObjectBasicProperties_RoomObject_PersistsImageVariantsAndChooserScript()
    {
        var target = new GameObject
        {
            Name = "Sign",
            ImageRotationDegrees = 0,
            ImageVariants =
            [
                new ObjectImageVariant
                {
                    VariantName = "default",
                    FullImagePath = "images/sign-default.png",
                    IsDefault = true
                }
            ],
            ImageVariantChooserScript = "return 'default';"
        };

        var room = new Room
        {
            Name = "Gallery",
            GameObjects = [target]
        };

        var area = new Area
        {
            Name = "Display",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Museum",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Earth",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "Variant Edit",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initial => initial with
            {
                ImageRotationDegrees = 180,
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = "images/sign-bright.png",
                        IsDefault = true
                    },
                    new ObjectImageVariant
                    {
                        VariantName = "faded",
                        FullImagePath = "images/sign-faded.png",
                        IsDefault = false
                    }
                ],
                ImageVariantChooserScript = "return object.IsHidden ? 'faded' : 'default';"
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(target, objectsNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        Assert.Equal(180, target.ImageRotationDegrees);
        Assert.Equal("return object.IsHidden ? 'faded' : 'default';", target.ImageVariantChooserScript);
        Assert.Collection(
            target.ImageVariants,
            variant =>
            {
                Assert.Equal("default", variant.VariantName);
                Assert.Equal("images/sign-bright.png", variant.FullImagePath);
                Assert.True(variant.IsDefault);
            },
            variant =>
            {
                Assert.Equal("faded", variant.VariantName);
                Assert.Equal("images/sign-faded.png", variant.FullImagePath);
                Assert.False(variant.IsDefault);
            });
    }

    [Fact]
    public void EditObjectBasicProperties_RoomObject_NormalizesVariantDefault_WhenMultipleDefaultsSubmitted()
    {
        var target = new GameObject
        {
            Name = "Switch",
            ImageVariants =
            [
                new ObjectImageVariant { VariantName = "off", FullImagePath = "images/switch-off.png", IsDefault = true },
                new ObjectImageVariant { VariantName = "on", FullImagePath = "images/switch-on.png", IsDefault = false }
            ]
        };

        var room = new Room
        {
            Name = "Control",
            GameObjects = [target]
        };

        var area = new Area
        {
            Name = "Main",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "HQ",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Earth",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "Variant Default Normalization",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initial => initial with
            {
                ImageVariants =
                [
                    new ObjectImageVariant { VariantName = "off", FullImagePath = "images/switch-off-v2.png", IsDefault = true },
                    new ObjectImageVariant { VariantName = "on", FullImagePath = "images/switch-on-v2.png", IsDefault = true },
                    new ObjectImageVariant { VariantName = "broken", FullImagePath = "images/switch-broken.png", IsDefault = false }
                ]
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(target, objectsNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        Assert.Equal(3, target.ImageVariants.Count);
        Assert.Collection(
            target.ImageVariants,
            variant =>
            {
                Assert.Equal("off", variant.VariantName);
                Assert.True(variant.IsDefault);
            },
            variant =>
            {
                Assert.Equal("on", variant.VariantName);
                Assert.False(variant.IsDefault);
            },
            variant =>
            {
                Assert.Equal("broken", variant.VariantName);
                Assert.False(variant.IsDefault);
            });
    }

    [Fact]
    public void EditObjectBasicProperties_RoomObject_CollapsesDuplicateVariantNames_PreservingFirstOccurrenceOrder()
    {
        var target = new GameObject
        {
            Name = "Monitor",
            ImageVariants =
            [
                new ObjectImageVariant { VariantName = "default", FullImagePath = "images/monitor-default.png", IsDefault = true }
            ]
        };

        var room = new Room
        {
            Name = "Ops",
            GameObjects = [target]
        };

        var area = new Area
        {
            Name = "Main",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "HQ",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Earth",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "Variant Duplicate Collapse",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initial => initial with
            {
                ImageVariants =
                [
                    new ObjectImageVariant { VariantName = "alert", FullImagePath = "images/monitor-alert-a.png", IsDefault = true },
                    new ObjectImageVariant { VariantName = "ALERT", FullImagePath = "images/monitor-alert-b.png", IsDefault = false },
                    new ObjectImageVariant { VariantName = "idle", FullImagePath = "images/monitor-idle.png", IsDefault = false }
                ]
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(target, objectsNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        Assert.Collection(
            target.ImageVariants,
            variant =>
            {
                Assert.Equal("alert", variant.VariantName);
                Assert.Equal("images/monitor-alert-a.png", variant.FullImagePath);
                Assert.True(variant.IsDefault);
            },
            variant =>
            {
                Assert.Equal("idle", variant.VariantName);
                Assert.Equal("images/monitor-idle.png", variant.FullImagePath);
                Assert.False(variant.IsDefault);
            });
    }
}
