using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class PromoteToObjectTemplateWorkflowTests
{
    [Fact]
    public void GetTreeContextActions_RoomObject_IncludesCreateTemplateFrom()
    {
        var project = BuildProject(out _, out var sourceNode);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var actions = viewModel.GetTreeContextActions(sourceNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "create-template-from-object", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromObject_CreatesTemplateWithoutLinkingSource()
    {
        var project = BuildProject(out _, out var sourceNode);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var source = sourceNode.GameObject;
        var previousActionCount = source.AvailableActions.Count;

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-object", sourceNode);

        Assert.True(handled);
        Assert.False(source.LinkedBaseObjectId.HasValue);
        Assert.False(source.LinkActionsToBaseObject);
        Assert.Equal(previousActionCount, source.AvailableActions.Count);

        var definition = Assert.Single(project.ObjectTemplates);
        Assert.NotEqual(source.ObjectId, definition.ObjectId);
        Assert.Equal(source.Name, definition.Name);
        Assert.Equal(previousActionCount, definition.AvailableActions.Count);
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromObject_AddsNodeToTemplateCatalogTree()
    {
        var project = BuildProjectModel(out var sourceObjectId);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-object", sourceNode);

        Assert.True(handled);

        var promotedDefinition = Assert.Single(project.ObjectTemplates);

        var templateCatalogNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<ObjectTemplatesNodeViewModel>()
            .Single(node => !node.IsBaseCatalog && node.Parent is ProjectRootNodeViewModel);

        Assert.Contains(
            templateCatalogNode.Children.OfType<TemplateGameObjectNodeViewModel>(),
            node => node.GameObject.ObjectId == promotedDefinition.ObjectId);
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromObject_WhenDuplicateExists_ShowsWarningAndDoesNotCreate()
    {
        var project = BuildProjectModel(out var sourceObjectId);
        var source = project.Planets
            .SelectMany(planet => planet.Countries)
            .SelectMany(country => country.Areas)
            .SelectMany(area => area.Rooms)
            .SelectMany(room => room.GameObjects)
            .Single(node => node.ObjectId == sourceObjectId);

        project.ObjectTemplates.Add(new GameObject
        {
            Name = source.Name,
            IsQuantifiable = true,
            QuantifiablePlacementDistributionMode = "GroupedStack",
            Quantity = 1
        });

        ScopeHierarchy.AttachParents(project);

        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == sourceObjectId);

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-object", sourceNode);

        Assert.False(handled);
        Assert.False(sourceNode.GameObject.LinkedBaseObjectId.HasValue);
        Assert.Single(project.ObjectTemplates);
        Assert.Contains(projectUi.WarningMessages, message => message.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromObject_UsesPromptedTemplateName()
    {
        var project = BuildProject(out _, out var sourceNode);
        var treeContext = new TreeContextInteractionServiceStub
        {
            TemplateNameFromObjectHandler = _ => "Reusable Marble Pattern"
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-object", sourceNode);

        Assert.True(handled);
        var definition = Assert.Single(project.ObjectTemplates);
        Assert.Equal("Reusable Marble Pattern", definition.Name);
        Assert.Equal("Marble Pile", sourceNode.GameObject.Name);
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromObject_WhenNamePromptCanceled_DoesNotCreate()
    {
        var project = BuildProject(out _, out var sourceNode);
        var treeContext = new TreeContextInteractionServiceStub
        {
            TemplateNameFromObjectHandler = _ => null
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-object", sourceNode);

        Assert.False(handled);
        Assert.Empty(project.ObjectTemplates);
    }

    [Fact]
    public void GetTreeContextActions_TemplateObject_UsesDeleteLabel()
    {
        var project = BuildProjectModel(out _);
        var template = new GameObject { Name = "Template Door" };
        project.ObjectTemplates.Add(template);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var templateNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<TemplateGameObjectNodeViewModel>()
            .Single(node => ReferenceEquals(node.GameObject, template));

        var actions = viewModel.GetTreeContextActions(templateNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "remove-object", StringComparison.Ordinal)
            && string.Equals(action.Header, "Delete Template Object", StringComparison.Ordinal));
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
            Name = "PromoteTemplateFixture",
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

    private static ProjectModel BuildProjectModel(out Guid sourceObjectId)
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
            Name = "PromoteTemplateFixture",
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

