using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelRoomImageEditingTests
{
    [Fact]
    public void AddNewObject_WhenRoomEditorOpen_RefreshesRoomDesignerObjectList()
    {
        var room = new Room { Name = "Room A" };
        var area = new Area { Name = "Area 1", Rooms = new List<Room> { room } };
        var country = new Country { Name = "Country 1", Areas = new List<Area> { area } };
        var planet = new Planet { Name = "Planet 1", Countries = new List<Country> { country } };
        var project = new ProjectModel { Name = "Project", Planets = new List<Planet> { planet } };

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initial => initial
        };
        var projectUi = new ProjectUiServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        var viewModel = bundle.ViewModel;

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);

        viewModel.OpenRoomEditor(room);
        Assert.NotNull(viewModel.SelectedRoomEditor);
        Assert.Empty(viewModel.SelectedRoomEditor!.RoomChildObjects);

        var handled = viewModel.ExecuteTreeContextAction("add-new-object", roomNode);

        Assert.True(handled);
        Assert.Single(room.GameObjects);
        Assert.Single(viewModel.SelectedRoomEditor.RoomChildObjects);
    }

    [Fact]
    public void EditRoomDesignerDirectionSlotCommand_OpensModelessEditor_ForSelectedRoomAndSlot()
    {
        var room = new Room { Name = "Room A" };
        var area = new Area { Name = "Area 1", Rooms = new List<Room> { room } };
        var country = new Country { Name = "Country 1", Areas = new List<Area> { area } };
        var planet = new Planet { Name = "Planet 1", Countries = new List<Country> { country } };
        var project = new ProjectModel { Name = "Project", Planets = new List<Planet> { planet } };

        var treeContext = new TreeContextInteractionServiceStub();
        var projectUi = new ProjectUiServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        var viewModel = bundle.ViewModel;

        viewModel.OpenRoomEditor(room);
        var slot = viewModel.SelectedRoomEditor?.NorthSlot;

        Assert.NotNull(slot);

        viewModel.EditRoomImageSlotCommand.Execute(slot);

        Assert.Equal(1, bundle.RoomDesignerDirectionImageDialogService.OpenOrFocusCallCount);
        Assert.Equal(room.Id, bundle.RoomDesignerDirectionImageDialogService.LastRoomId);
        Assert.Same(slot, bundle.RoomDesignerDirectionImageDialogService.LastSlot);
    }

    [Fact]
    public void OpenRoomEditor_SwitchingRooms_ClosesOpenModelessEditors()
    {
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var area = new Area { Name = "Area 1", Rooms = new List<Room> { roomA, roomB } };
        var country = new Country { Name = "Country 1", Areas = new List<Area> { area } };
        var planet = new Planet { Name = "Planet 1", Countries = new List<Country> { country } };
        var project = new ProjectModel { Name = "Project", Planets = new List<Planet> { planet } };

        var treeContext = new TreeContextInteractionServiceStub();
        var projectUi = new ProjectUiServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        var viewModel = bundle.ViewModel;

        viewModel.OpenRoomEditor(roomA);
        var closeCountAfterFirstSelection = bundle.RoomDesignerDirectionImageDialogService.CloseAllEditorsCallCount;

        viewModel.OpenRoomEditor(roomB);

        Assert.Equal(closeCountAfterFirstSelection + 1, bundle.RoomDesignerDirectionImageDialogService.CloseAllEditorsCallCount);
    }

    [Fact]
    public void RefreshSelectedRoomEditorCommand_RebuildsRoomObjectListFromModel()
    {
        var room = new Room { Name = "Room A" };
        var area = new Area { Name = "Area 1", Rooms = new List<Room> { room } };
        var country = new Country { Name = "Country 1", Areas = new List<Area> { area } };
        var planet = new Planet { Name = "Planet 1", Countries = new List<Country> { country } };
        var project = new ProjectModel { Name = "Project", Planets = new List<Planet> { planet } };

        var treeContext = new TreeContextInteractionServiceStub();
        var projectUi = new ProjectUiServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        var viewModel = bundle.ViewModel;

        viewModel.OpenRoomEditor(room);
        Assert.NotNull(viewModel.SelectedRoomEditor);
        Assert.Empty(viewModel.SelectedRoomEditor!.RoomChildObjects);

        room.GameObjects.Add(new GameObject { Name = "Late Added" });

        viewModel.RefreshSelectedRoomEditorCommand.Execute(null);

        Assert.Single(viewModel.SelectedRoomEditor.RoomChildObjects);
        Assert.Equal("Late Added", viewModel.SelectedRoomEditor.RoomChildObjects[0].GameObject.Name);
    }

    [Fact]
    public void CloseRoomEditorCommand_RemovesTabAndKeepsAnotherTabSelected()
    {
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var area = new Area { Name = "Area 1", Rooms = new List<Room> { roomA, roomB } };
        var country = new Country { Name = "Country 1", Areas = new List<Area> { area } };
        var planet = new Planet { Name = "Planet 1", Countries = new List<Country> { country } };
        var project = new ProjectModel { Name = "Project", Planets = new List<Planet> { planet } };

        var treeContext = new TreeContextInteractionServiceStub();
        var projectUi = new ProjectUiServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        var viewModel = bundle.ViewModel;

        viewModel.OpenRoomEditor(roomA);
        var roomAEditor = viewModel.SelectedRoomEditor;
        Assert.NotNull(roomAEditor);

        viewModel.OpenRoomEditor(roomB);
        var roomBEditor = viewModel.SelectedRoomEditor;
        Assert.NotNull(roomBEditor);
        Assert.Equal(2, viewModel.OpenRoomEditors.Count);

        viewModel.CloseRoomEditorCommand.Execute(roomAEditor);

        Assert.Single(viewModel.OpenRoomEditors);
        Assert.Same(roomBEditor, viewModel.SelectedRoomEditor);
        Assert.Equal(roomB.Id, viewModel.OpenRoomEditors[0].Room.Id);
    }
}
