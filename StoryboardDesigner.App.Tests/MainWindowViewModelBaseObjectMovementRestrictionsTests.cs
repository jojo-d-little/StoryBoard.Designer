using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelBaseObjectMovementRestrictionsTests
{
    [Fact]
    public void EditObjectBasicProperties_RoundTripsParentCueInheritanceSelection()
    {
        var child = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flame",
            IsPartOfParent = true,
            Placement = new StructuralPartPlacement { Mode = StructuralPartPlacementKind.ParentRelative }
        };
        var parent = new GameObject { ObjectId = Guid.NewGuid(), Name = "Hearth", IsContainer = true, ContainedObjects = [child] };
        var room = new Room { Name = "Room", GameObjects = [parent] };
        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "InheritanceEdit", Planets = [planet] };
        ScopeHierarchy.AttachParents(project);
        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                InheritParentCueCategories = ["Movement", "Highlight", "Appearance", "Disappearance"]
            }
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var parentNode = new GameObjectNodeViewModel(parent, objectsNode);
        var childNode = new GameObjectNodeViewModel(child, parentNode);

        Assert.True(viewModel.EditObjectBasicProperties(childNode));

        var request = Assert.IsType<ObjectBasicPropertiesEditRequest>(treeContext.LastObjectBasicPropertiesRequest);
        Assert.Null(request.InheritParentCueCategories);
        Assert.Equal(new[] { "Movement", "Highlight", "Appearance", "Disappearance" }, child.InheritParentCueCategories);
    }

    [Fact]
    public void EditObjectBasicProperties_UnlinkedTemplateObject_UpdatesMovementRestrictionCategoryRules()
    {
        var templateObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "TemplateObject",
            MovementRestrictions = BuildMovementRestrictions(maxDistance: 1)
        };

        var project = new ProjectModel
        {
            ObjectTemplates = [templateObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                MovementRestrictions = BuildMovementRestrictions(maxDistance: 3)
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var templatesNode = new ObjectTemplatesNodeViewModel(project, rootNode);
        var objectNode = new TemplateGameObjectNodeViewModel(templateObject, templatesNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        Assert.NotNull(templateObject.MovementRestrictions);
        Assert.Equal(3, templateObject.MovementRestrictions!.FirstStacked.E.MaxDistance);
        Assert.Equal(1, templateObject.MovementRestrictions.SubsequentStacked.E.MaxDistance);
    }

    [Fact]
    public void EditObjectBasicProperties_SelfLinkedTemplateObject_UpdatesMovementRestrictionCategoryRules()
    {
        var templateObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "TemplateObject",
            MovementRestrictions = BuildMovementRestrictions(maxDistance: 1)
        };

        // Simulate stale persisted metadata where a definition points to itself.
        templateObject.LinkedBaseObjectId = templateObject.ObjectId;
        templateObject.LinkActionsToBaseObject = true;

        var project = new ProjectModel
        {
            ObjectTemplates = [templateObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                MovementRestrictions = BuildMovementRestrictions(maxDistance: 4)
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var templatesNode = new ObjectTemplatesNodeViewModel(project, rootNode);
        var objectNode = new TemplateGameObjectNodeViewModel(templateObject, templatesNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        Assert.NotNull(templateObject.MovementRestrictions);
        Assert.Equal(4, templateObject.MovementRestrictions!.FirstStacked.E.MaxDistance);
        Assert.Equal(1, templateObject.MovementRestrictions.SubsequentStacked.E.MaxDistance);
    }

    [Fact]
    public void EditObjectBasicProperties_GlobalBaseObject_UpdatesMovementRestrictionCategoryRules()
    {
        var baseObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "BaseObject",
            IsMovable = true,
            MovementRestrictions = BuildMovementRestrictions(maxDistance: 1)
        };

        var project = new ProjectModel
        {
            GlobalObjectScopeName = "Global Objects",
            GameObjects = [baseObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                MovementRestrictions = BuildMovementRestrictions(maxDistance: 5)
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var globalNode = new GlobalObjectsNodeViewModel(project, rootNode);
        var objectNode = new GlobalObjectNodeViewModel(baseObject, globalNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        Assert.NotNull(baseObject.MovementRestrictions);
        Assert.Equal(5, baseObject.MovementRestrictions!.FirstStacked.E.MaxDistance);
        Assert.Equal(1, baseObject.MovementRestrictions.SubsequentStacked.E.MaxDistance);
    }

    [Fact]
    public void EditObjectBasicProperties_TemplateObject_PreservesPassiveSpatialTypeInDialogRequest()
    {
        var templateObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "TemplateObject",
            SpatialType = RuntimeObjectSpatialTypes.PassiveObject
        };

        var project = new ProjectModel
        {
            ObjectTemplates = [templateObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var templatesNode = new ObjectTemplatesNodeViewModel(project, rootNode);
        var objectNode = new TemplateGameObjectNodeViewModel(templateObject, templatesNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        var request = Assert.IsType<ObjectBasicPropertiesEditRequest>(treeContext.LastObjectBasicPropertiesRequest);
        Assert.Equal(RuntimeObjectSpatialTypes.PassiveObject, request.SpatialType);
    }

    [Fact]
    public void EditObjectBasicProperties_BaseCatalogObject_RoundTripsLightingThroughDialog()
    {
        var baseObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Lit Base Object",
            PointLight = new PointLight { RadiusPx = 120, Color = "#FFE080" },
            LightOcclusion = new LightOcclusion { Strength = 0.5 },
            IsPointLightEnabledDefaultValue = false
        };
        var project = new ProjectModel { BaseObjects = [baseObject] };
        ScopeHierarchy.AttachParents(project);
        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                PointLight = new PointLight { RadiusPx = 240, Color = "#FF8040" },
                LightOcclusion = new LightOcclusion { Strength = 0.25 },
                IsPointLightEnabledDefaultValue = true
            }
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        var baseCatalogNode = new ObjectTemplatesNodeViewModel(project, rootNode, isBaseCatalog: true);
        var objectNode = new TemplateGameObjectNodeViewModel(baseObject, baseCatalogNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        var request = Assert.IsType<ObjectBasicPropertiesEditRequest>(treeContext.LastObjectBasicPropertiesRequest);
        Assert.Equal(120, request.PointLight!.RadiusPx);
        Assert.Equal("#FFE080", request.PointLight.Color);
        Assert.Equal(0.5, request.LightOcclusion!.Strength);
        Assert.False(request.IsPointLightEnabledDefaultValue);
        Assert.Equal(240, baseObject.PointLight!.RadiusPx);
        Assert.Equal("#FF8040", baseObject.PointLight.Color);
        Assert.Equal(0.25, baseObject.LightOcclusion!.Strength);
        Assert.True(baseObject.IsPointLightEnabledDefaultValue);
    }

    [Fact]
    public void EditObjectBasicProperties_GlobalBaseObject_CanUpdateSpatialTypeToPassive()
    {
        var baseObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "BaseObject",
            SpatialType = RuntimeObjectSpatialTypes.SolidObject,
            StackGroup = 3
        };

        var project = new ProjectModel
        {
            GlobalObjectScopeName = "Global Objects",
            GameObjects = [baseObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                SpatialType = RuntimeObjectSpatialTypes.PassiveObject
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var globalNode = new GlobalObjectsNodeViewModel(project, rootNode);
        var objectNode = new GlobalObjectNodeViewModel(baseObject, globalNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        Assert.Equal(RuntimeObjectSpatialTypes.PassiveObject, baseObject.SpatialType);
        Assert.Equal(0, baseObject.StackGroup);
    }

    private static ObjectMovementRestrictions BuildMovementRestrictions(int maxDistance)
    {
        return new ObjectMovementRestrictions
        {
            FirstStacked = new ObjectMovementRestrictionCategory
            {
                E = new ObjectMovementRestrictionRule
                {
                    MaxDistance = maxDistance,
                    AllowJumpOver = false
                }
            },
            SubsequentStacked = new ObjectMovementRestrictionCategory
            {
                E = new ObjectMovementRestrictionRule
                {
                    MaxDistance = 1,
                    AllowJumpOver = false
                }
            }
        };
    }
}
