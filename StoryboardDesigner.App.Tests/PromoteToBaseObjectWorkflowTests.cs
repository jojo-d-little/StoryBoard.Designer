using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class PromoteToBaseObjectWorkflowTests
{
    [Fact]
    public void GetTreeContextActions_RoomObject_IncludesPromoteToBaseObject()
    {
        var project = BuildProject(out var roomNode, out var sourceNode);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var actions = viewModel.GetTreeContextActions(sourceNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "promote-to-base-object", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_CreatesAreaBaseAndConvertsPlacedObjectToInstance()
    {
        var project = BuildProject(out var roomNode, out var sourceNode);
        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => BaseObjectPromotionScopeKind.Area
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var source = sourceNode.GameObject;
        var previousActionCount = source.AvailableActions.Count;

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.True(handled);
        Assert.True(source.LinkedBaseObjectId.HasValue);
        Assert.True(source.LinkActionsToBaseObject);
        Assert.Empty(source.AvailableActions);

        var area = roomNode.Area;
        var definition = Assert.Single(area.BaseObjects);

        Assert.Equal(source.LinkedBaseObjectId.Value, definition.ObjectId);
        Assert.Equal(source.Name, definition.Name);
        Assert.Equal(previousActionCount, definition.AvailableActions.Count);
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_WhenFailureInjected_RollsBackModelChanges()
    {
        var project = BuildProject(out var roomNode, out var sourceNode);
        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => BaseObjectPromotionScopeKind.Area
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var source = sourceNode.GameObject;
        var area = roomNode.Area;
        var originalLinkedBaseObjectId = source.LinkedBaseObjectId;
        var originalLinkActionsToBase = source.LinkActionsToBaseObject;
        var originalActions = source.AvailableActions.ToList();

        var field = typeof(MainWindowViewModel).GetField(
            "PromoteToBaseObjectFailureInjection",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        field!.SetValue(null, (Func<GameObject, Exception?>)(_ => new InvalidOperationException("Injected promotion failure.")));

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode));
        }
        finally
        {
            field.SetValue(null, null);
        }

        Assert.Equal(originalLinkedBaseObjectId, source.LinkedBaseObjectId);
        Assert.Equal(originalLinkActionsToBase, source.LinkActionsToBaseObject);
        Assert.Equal(originalActions.Count, source.AvailableActions.Count);
        Assert.Empty(area.BaseObjects);
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_AddsNodeToAreaBaseObjectsTree()
    {
        var project = BuildProjectModel(out var areaId, out var sourceObjectId);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => BaseObjectPromotionScopeKind.Area
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.True(handled);

        var area = project.Planets
            .SelectMany(planet => planet.Countries)
            .SelectMany(country => country.Areas)
            .Single(candidate => candidate.Id == areaId);

        var promotedDefinition = Assert.Single(area.BaseObjects);

        var areaBaseCatalogNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<ObjectTemplatesNodeViewModel>()
            .Single(node => node.IsBaseCatalog && ReferenceEquals(node.CatalogParentScope, area));

        Assert.Contains(
            areaBaseCatalogNode.Children.OfType<TemplateGameObjectNodeViewModel>(),
            node => node.GameObject.ObjectId == promotedDefinition.ObjectId);
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_WhenCountrySelected_AddsDefinitionToCountryCatalog()
    {
        var project = BuildProjectModel(out _, out var sourceObjectId);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => BaseObjectPromotionScopeKind.Country
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.True(handled);

        var country = Assert.Single(project.Planets)
            .Countries
            .Single();

        var promotedDefinition = Assert.Single(country.BaseObjects);
        Assert.Empty(Assert.Single(country.Areas).BaseObjects);
        Assert.Equal(promotedDefinition.ObjectId, sourceNode.GameObject.LinkedBaseObjectId);
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_WhenPlanetSelected_AddsDefinitionToPlanetCatalog()
    {
        var project = BuildProjectModel(out _, out var sourceObjectId);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => BaseObjectPromotionScopeKind.Planet
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.True(handled);

        var planet = Assert.Single(project.Planets);
        var promotedDefinition = Assert.Single(planet.BaseObjects);
        var country = Assert.Single(planet.Countries);
        Assert.Empty(country.BaseObjects);
        Assert.Empty(Assert.Single(country.Areas).BaseObjects);
        Assert.Equal(promotedDefinition.ObjectId, sourceNode.GameObject.LinkedBaseObjectId);
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_WhenGlobalSelected_AddsDefinitionToGlobalCatalog()
    {
        var project = BuildProjectModel(out _, out var sourceObjectId);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => BaseObjectPromotionScopeKind.Global
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.True(handled);

        var promotedDefinition = Assert.Single(project.BaseObjects);
        var planet = Assert.Single(project.Planets);
        var country = Assert.Single(planet.Countries);
        Assert.Empty(planet.BaseObjects);
        Assert.Empty(country.BaseObjects);
        Assert.Empty(Assert.Single(country.Areas).BaseObjects);
        Assert.Equal(promotedDefinition.ObjectId, sourceNode.GameObject.LinkedBaseObjectId);
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_WhenDuplicateExistsInSelectedScope_ShowsWarningAndDoesNotPromote()
    {
        var project = BuildProjectModel(out _, out var sourceObjectId);
        var source = project.Planets
            .SelectMany(planet => planet.Countries)
            .SelectMany(country => country.Areas)
            .SelectMany(area => area.Rooms)
            .SelectMany(room => room.GameObjects)
            .Single(node => node.ObjectId == sourceObjectId);

        project.BaseObjects.Add(new GameObject
        {
            Name = source.Name,
            IsQuantifiable = true,
            QuantifiablePlacementDistributionMode = "GroupedStack",
            Quantity = 1
        });

        ScopeHierarchy.AttachParents(project);

        var projectUi = new ProjectUiServiceStub();
        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => BaseObjectPromotionScopeKind.Global
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            projectUi);

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.False(handled);
        Assert.False(sourceNode.GameObject.LinkedBaseObjectId.HasValue);
        Assert.Single(project.BaseObjects);
        Assert.Contains(projectUi.WarningMessages, message => message.Contains("already exists in Global", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_WhenScopeSelectionCanceled_LeavesModelUnchanged()
    {
        var project = BuildProjectModel(out _, out var sourceObjectId);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => null
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var source = sourceNode.GameObject;
        var originalLinkedBaseObjectId = source.LinkedBaseObjectId;
        var originalLinkActionsToBase = source.LinkActionsToBaseObject;
        var originalActionsCount = source.AvailableActions.Count;
        var baseCatalogNodes = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<ObjectTemplatesNodeViewModel>()
            .Where(node => node.IsBaseCatalog)
            .ToList();
        var originalTreeBaseObjectCount = baseCatalogNodes.Sum(node => node.Children.Count);

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.False(handled);
        Assert.Equal(originalLinkedBaseObjectId, source.LinkedBaseObjectId);
        Assert.Equal(originalLinkActionsToBase, source.LinkActionsToBaseObject);
        Assert.Equal(originalActionsCount, source.AvailableActions.Count);
        Assert.Equal(4, treeContext.LastBaseObjectPromotionScopeOptions.Count);
        Assert.Equal(originalTreeBaseObjectCount, baseCatalogNodes.Sum(node => node.Children.Count));
        Assert.All(baseCatalogNodes, node => Assert.Empty(node.Children));
        Assert.Empty(project.BaseObjects);

        var planet = Assert.Single(project.Planets);
        Assert.Empty(planet.BaseObjects);
        var country = Assert.Single(planet.Countries);
        Assert.Empty(country.BaseObjects);
        Assert.Empty(Assert.Single(country.Areas).BaseObjects);
    }

    [Fact]
    public void ExecuteTreeContextAction_PromoteToBaseObject_ScopePickerOptions_AreOrderedAndLabeled()
    {
        var project = BuildProjectModel(out _, out var sourceObjectId);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            BaseObjectPromotionScopeSelectionHandler = _ => null
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("promote-to-base-object", sourceNode);

        Assert.False(handled);

        var options = treeContext.LastBaseObjectPromotionScopeOptions;
        Assert.Equal(4, options.Count);

        Assert.Equal(BaseObjectPromotionScopeKind.Area, options[0].ScopeKind);
        Assert.Equal("Area: Area A", options[0].Label);

        Assert.Equal(BaseObjectPromotionScopeKind.Country, options[1].ScopeKind);
        Assert.Equal("Country: Country A", options[1].Label);

        Assert.Equal(BaseObjectPromotionScopeKind.Planet, options[2].ScopeKind);
        Assert.Equal("Planet: Planet A", options[2].Label);

        Assert.Equal(BaseObjectPromotionScopeKind.Global, options[3].ScopeKind);
        Assert.Equal("Global", options[3].Label);
    }

    private static ProjectModel BuildProject(out RoomNodeViewModel roomNode, out GameObjectNodeViewModel sourceNode)
    {
        var sourceObject = new GameObject
        {
            Name = "Marble Pile",
            IsQuantifiable = true,
            QuantifiablePlacementDistributionMode = "GroupedStack",
            Quantity = 3,
            AvailableActions =
            [
                new CommandAction
                {
                    Name = "look",
                    ActionType = CommandActionType.EchoMessage,
                    EchoMessage = "Looks like marbles.",
                    Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                }
            ]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [sourceObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Name = "PromoteFixture",
            GlobalObjectScopeName = "Global Objects",
            Planets = [planet]
        };

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        sourceNode = new GameObjectNodeViewModel(sourceObject, roomObjectsNode);

        return project;
    }

    private static ProjectModel BuildProjectModel(out Guid areaId, out Guid sourceObjectId)
    {
        var sourceObject = new GameObject
        {
            Name = "Marble Pile",
            IsQuantifiable = true,
            QuantifiablePlacementDistributionMode = "GroupedStack",
            Quantity = 3,
            AvailableActions =
            [
                new CommandAction
                {
                    Name = "look",
                    ActionType = CommandActionType.EchoMessage,
                    EchoMessage = "Looks like marbles.",
                    Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                }
            ]
        };

        sourceObjectId = sourceObject.ObjectId;

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [sourceObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        areaId = area.Id;

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        return new ProjectModel
        {
            Name = "PromoteFixture",
            GlobalObjectScopeName = "Global Objects",
            Planets = [planet]
        };
    }

    private static void InvokeLoadProjectIntoHierarchy(MainWindowViewModel vm)
    {
        var method = typeof(MainWindowViewModel).GetMethod("LoadProjectIntoHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(vm, null);
    }

    private static IEnumerable<HierarchyNodeViewModel> EnumerateNodes(IEnumerable<HierarchyNodeViewModel> roots)
    {
        var stack = new Stack<HierarchyNodeViewModel>(roots.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }
}


