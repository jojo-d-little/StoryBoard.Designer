using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelBaseObjectMovementRestrictionsTests
{
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
