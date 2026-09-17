using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class LinkedInstanceActionContextMenuTests
{
    [Fact]
    public void GetTreeContextActions_LinkedRoomInstanceObject_HidesEditScopedActions()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Base Flowers"
        };

        var linkedObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Linked Flowers",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject, linkedObject]
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
            Name = "Context Menu Test",
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var projectNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, projectNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var linkedNode = new GameObjectNodeViewModel(linkedObject, objectsNode);

        var actions = viewModel.GetTreeContextActions(linkedNode);

        Assert.DoesNotContain(actions, action => action.ActionId == "edit-scoped-actions");
        Assert.DoesNotContain(actions, action => action.ActionId == "edit-scoped-verbs");
        Assert.DoesNotContain(actions, action => action.ActionId == "edit-scoped-directionals");
        Assert.Contains(actions, action => action.ActionId == "go-to-base-object");
    }

    [Fact]
    public void GetTreeContextActions_BaseObjectAndRoomScope_KeepEditScopedActions()
    {
        var baseObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Base Flowers"
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
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
            Name = "Context Menu Test",
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var projectNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, projectNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var baseNode = new GameObjectNodeViewModel(baseObject, objectsNode);
        var roomActionsNode = new ScopedActionsNodeViewModel(PropertyResolutionScope.Room, room.AvailableActions, roomNode);

        var baseActions = viewModel.GetTreeContextActions(baseNode);
        var roomScopeActions = viewModel.GetTreeContextActions(roomActionsNode);

        Assert.Contains(baseActions, action => action.ActionId == "edit-scoped-actions");
        Assert.Contains(roomScopeActions, action => action.ActionId == "edit-scoped-actions");
    }

    [Fact]
    public void ExecuteTreeContextAction_GoToBaseObject_SelectsBaseNode()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Base Flowers"
        };

        var linkedObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Linked Flowers",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
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
            Name = "Context Menu Test",
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var linkedNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == linkedObject.ObjectId);

        var handled = viewModel.ExecuteTreeContextAction("go-to-base-object", linkedNode);

        Assert.True(handled);
        var selectedBaseNode = Assert.IsType<GameObjectNodeViewModel>(viewModel.SelectedNode);
        Assert.Equal(baseObjectId, selectedBaseNode.GameObject.ObjectId);
    }

    [Fact]
    public void ExecuteTreeContextAction_LinkedObjectVerbsAndDirectionals_ShowLockedInformationAndReturnFalse()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Base Flowers"
        };

        var linkedObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Linked Flowers",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
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
            Name = "Context Menu Test",
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        var linkedNode = new GameObjectNodeViewModel(linkedObject, new RoomGameObjectsNodeViewModel(new RoomNodeViewModel(roomB, area, new AreaNodeViewModel(area, new CountryNodeViewModel(country, new PlanetNodeViewModel(project, planet, new ProjectRootNodeViewModel(project)))))));

        var verbsHandled = viewModel.ExecuteTreeContextAction("edit-scoped-verbs", linkedNode);
        var directionalsHandled = viewModel.ExecuteTreeContextAction("edit-scoped-directionals", linkedNode);

        Assert.False(verbsHandled);
        Assert.False(directionalsHandled);
        Assert.Contains(projectUi.InformationMessages, message => message.Contains("Edit verbs on the base item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(projectUi.InformationMessages, message => message.Contains("Edit directionals on the base item", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExecuteTreeContextAction_EditScopedVerbs_UsesInvokedNodeWhenAnotherNodeIsSelected()
    {
        var room = new Room { Name = "Room A" };
        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "Context Menu Test",
            CommandVerbs = ["look"],
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditScopedTokenListHandler = (scopeLabel, tokenKind, values, _, _) =>
            {
                Assert.Equal("Global", scopeLabel);
                Assert.Equal("Verbs", tokenKind);
                values.Add("inspect");
                return true;
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var roomNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Single();
        var globalVerbsNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<ScopedVerbsNodeViewModel>()
            .Single(node => node.Scope == PropertyResolutionScope.Global);

        viewModel.SelectedNode = roomNode;

        var handled = viewModel.ExecuteTreeContextAction("edit-scoped-verbs", globalVerbsNode);

        Assert.True(handled);
        Assert.Same(globalVerbsNode, viewModel.SelectedNode);
        Assert.Contains("inspect", project.CommandVerbs);
        Assert.Contains(globalVerbsNode.Children.OfType<ScopedVerbEntryNodeViewModel>(), child =>
            string.Equals(child.Verb, "inspect", StringComparison.OrdinalIgnoreCase));
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
