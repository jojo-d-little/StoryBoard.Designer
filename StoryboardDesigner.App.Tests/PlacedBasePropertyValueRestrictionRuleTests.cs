using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Rules.Project;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class PlacedBasePropertyValueRestrictionRuleTests
{
    [Fact]
    public void LocalValueSavedBeforeBaseRestrictionChange_IsValidatedAgainstEffectiveBaseRestrictionWithoutMutation()
    {
        var sharedId = Guid.NewGuid();
        var baseProperty = new GamePropertyDefinition
        {
            Name = "heading",
            DefaultValue = "north",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted,
            SharedVariableId = sharedId
        };
        var (project, baseObject, placement) = BuildLinkedPlacement(baseProperty);
        var node = CreatePlacementPropertyNode(project, placement);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        Assert.True(viewModel.SaveInstancePropertyValue(node, "north"));
        var local = Assert.Single(placement.Variables);
        var localId = local.Id;

        baseProperty.ValueRestriction = GamePropertyValueRestriction.Numeric;
        baseProperty.DefaultValue = "0";
        var dirtyBeforeValidation = viewModel.IsProjectDirty;
        var issues = new PlacedBasePropertyValueRestrictionRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("Elevator A.Game Properties.heading", issue.Path);
        Assert.Contains("must be numeric", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Same(local, Assert.Single(placement.Variables));
        Assert.Equal(localId, local.Id);
        Assert.Equal("north", local.DefaultValue);
        Assert.Equal(sharedId, local.SharedVariableId);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, local.ValueRestriction);
        Assert.Equal("0", baseProperty.DefaultValue);
        Assert.Equal(GamePropertyValueRestriction.Numeric, baseProperty.ValueRestriction);
        Assert.Equal(dirtyBeforeValidation, viewModel.IsProjectDirty);
        Assert.Equal(baseObject.ObjectId, placement.LinkedBaseObjectId);
    }

    [Fact]
    public void CompatibleRetainedLocalValue_RemainsValidAfterBaseRestrictionChange()
    {
        var baseProperty = new GamePropertyDefinition
        {
            Name = "homeFloor",
            DefaultValue = "unset",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted
        };
        var (project, _, placement) = BuildLinkedPlacement(baseProperty);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        Assert.True(viewModel.SaveInstancePropertyValue(CreatePlacementPropertyNode(project, placement), "42"));
        baseProperty.ValueRestriction = GamePropertyValueRestriction.Numeric;
        baseProperty.DefaultValue = "0";

        var issues = new PlacedBasePropertyValueRestrictionRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        Assert.Empty(issues);
        Assert.Equal("42", Assert.Single(placement.Variables).DefaultValue);
    }

    [Fact]
    public void CorrectingLocalValueClearsIssueAndValidationPreservesRecordAndMembership()
    {
        var sharedId = Guid.NewGuid();
        var baseProperty = new GamePropertyDefinition
        {
            Name = "heading",
            DefaultValue = "north",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted,
            SharedVariableId = sharedId
        };
        var (project, _, placement) = BuildLinkedPlacement(baseProperty);
        var node = CreatePlacementPropertyNode(project, placement);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        Assert.True(viewModel.SaveInstancePropertyValue(node, "north"));
        var local = Assert.Single(placement.Variables);
        var localId = local.Id;
        baseProperty.ValueRestriction = GamePropertyValueRestriction.Numeric;
        baseProperty.DefaultValue = "0";

        Assert.Single(new PlacedBasePropertyValueRestrictionRule().Evaluate(ValidationRuleTestContext.Create(project)));
        Assert.Equal(localId, local.Id);
        Assert.Equal("north", local.DefaultValue);
        Assert.Equal(sharedId, local.SharedVariableId);
        Assert.Equal("0", baseProperty.DefaultValue);
        Assert.Equal(GamePropertyValueRestriction.Numeric, baseProperty.ValueRestriction);

        Assert.True(viewModel.SaveInstancePropertyValue(node, "12.5"));
        var issues = new PlacedBasePropertyValueRestrictionRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        Assert.Empty(issues);
        Assert.Same(local, Assert.Single(placement.Variables));
        Assert.Equal(localId, local.Id);
        Assert.Equal("12.5", local.DefaultValue);
        Assert.Equal(sharedId, local.SharedVariableId);
        Assert.Equal("0", baseProperty.DefaultValue);
        Assert.Equal(GamePropertyValueRestriction.Numeric, baseProperty.ValueRestriction);
    }

    private static (ProjectModel Project, GameObject BaseObject, GameObject Placement) BuildLinkedPlacement(GamePropertyDefinition baseProperty)
    {
        var baseObject = new GameObject { Name = "Elevator", Variables = [baseProperty] };
        var placement = new GameObject { Name = "Elevator A", LinkedBaseObjectId = baseObject.ObjectId };
        var room = new Room { Name = "Foyer", GameObjects = [placement] };
        var project = new ProjectModel
        {
            BaseObjects = [baseObject],
            Planets = [new Planet { Name = "Earth", Countries = [new Country { Name = "Country", Areas = [new Area { Name = "Area", Rooms = [room] }] }] }]
        };
        ScopeHierarchy.AttachParents(project);
        return (project, baseObject, placement);
    }

    private static GamePropertyNodeViewModel CreatePlacementPropertyNode(ProjectModel project, GameObject placement)
    {
        ScopeHierarchy.AttachParents(project);
        var root = new ProjectRootNodeViewModel(project);
        var method = typeof(MainWindowViewModel).GetMethod("CreateVariablesNode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var container = Assert.IsType<GamePropertiesContainerNodeViewModel>(method.Invoke(null,
            [root, placement, placement.Variables, PropertyResolutionScope.Object, null, null]));
        return Assert.Single(container.Children.OfType<GamePropertyNodeViewModel>());
    }
}
