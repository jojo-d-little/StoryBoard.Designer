using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Tests;

public sealed class SetGamePropertyValueSourceTests
{
    [Fact]
    public void UpdatingDestination_PreservesSelectedValueSource_AndLegacyLiteralHasNoSource()
    {
        var source = new ProjectGamePropertySourceReferenceDto
        {
            AnchorKey = "currentRoom",
            PropertyName = "floorNumber"
        };
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetGameProperty,
            Payload = new SetGamePropertyPayload("oldDestination", "unchanged literal", null, source)
        };

        ActionPayloadAccessors.SetSetGameProperty(action, "newDestination", "unchanged literal");
        var updated = Assert.IsType<SetGamePropertyPayload>(action.Payload);
        Assert.Equal("floorNumber", updated.PropertyValueSourceReference?.PropertyName);
        Assert.Equal("currentRoom", updated.PropertyValueSourceReference?.AnchorKey);
        Assert.Equal("unchanged literal", updated.PropertyValue);
        Assert.Contains("from currentRoom.floorNumber", CommandActionSummaryPresenter.BuildSummary(action));

        ActionPayloadAccessors.SetSetGamePropertyValueSourceReference(action, null);
        Assert.Null(Assert.IsType<SetGamePropertyPayload>(action.Payload).PropertyValueSourceReference);
    }

    [Fact]
    public void SharedPicker_AllowsCatalogBackedReadOnlyCurrentRoomFloorAsSourceButNotDestination()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var (project, room) = BuildProjectWithRoom();
                var choices = MainWindowViewModel.BuildComputedPropertyChoices(project, room);
                var floorChoice = Assert.Single(choices, choice => choice.OwnerId == room.Id && choice.PropertyName == "floorNumber");
                Assert.True(floorChoice.IsEngineProperty);
                Assert.False(floorChoice.CanWrite);
                Assert.Contains("currentRoom", floorChoice.EligibleAnchors);
                Assert.Contains(floorChoice, ActionPropertyChoiceFilter.Filter(choices));
                Assert.DoesNotContain(floorChoice, ActionPropertyChoiceFilter.Filter(choices, requireWritable: true));

                var context = new GamePropertyPickerContext(
                    project,
                    room,
                    PropertyResolutionScope.Room,
                    _ => ActionPropertyChoiceFilter.Filter(choices),
                    AllowReferenceModes: true,
                    RelativeOwnerId: room.Id,
                    AnchorKeys: ["currentRoom"]);
                var picker = new VariableChooserDialog(context, "Choose value source");
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    var grid = (DataGrid)typeof(VariableChooserDialog)
                        .GetField("ChoicesDataGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                    var mode = (ComboBox)typeof(VariableChooserDialog)
                        .GetField("ReferenceModeComboBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                    var anchor = (ComboBox)typeof(VariableChooserDialog)
                        .GetField("AnchorComboBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                    mode.SelectedIndex = 2;
                    anchor.SelectedItem = "currentRoom";
                    grid.SelectedItem = floorChoice;
                    typeof(VariableChooserDialog)
                        .GetMethod("Select_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(picker, [picker, new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)]);
                };
                timer.Start();

                Assert.True(picker.ShowDialog());
                Assert.Equal("currentRoom", picker.SelectedReference?.AnchorKey);
                Assert.Equal("floorNumber", picker.SelectedReference?.PropertyName);
                Assert.Null(picker.SelectedReference?.OwnerId);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(SharedPicker_AllowsCatalogBackedReadOnlyCurrentRoomFloorAsSourceButNotDestination));

        if (failure is not null) throw failure;
    }

    [Fact]
    public void SaveLoadAndRuntimeExport_PreserveSourceAndLegacyLiteralSetGamePropertyActions()
    {
        var (project, room) = BuildProjectWithRoom();
        var destination = new ProjectGamePropertySourceReferenceDto
        {
            OwnerId = room.Id,
            PropertyName = "authoredDestination"
        };
        var floorSource = new ProjectGamePropertySourceReferenceDto
        {
            AnchorKey = "currentRoom",
            PropertyName = "floorNumber"
        };
        var sourcedAction = new CommandAction
        {
            Name = "Copy floor",
            ActionType = CommandActionType.SetGameProperty,
            Payload = new SetGamePropertyPayload("authoredDestination", "legacy fallback", destination, floorSource)
        };
        var literalAction = new CommandAction
        {
            Name = "Legacy literal",
            ActionType = CommandActionType.SetGameProperty,
            Payload = new SetGamePropertyPayload("authoredDestination", "still literal", destination)
        };
        room.AvailableActions = [sourcedAction, literalAction];
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "SetPropertySource.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, project);
            var reopened = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var loadedRoom = Assert.Single(Assert.Single(Assert.Single(Assert.Single(reopened.Planets).Countries).Areas).Rooms);
            var loadedSourceAction = loadedRoom.AvailableActions.Single(action => action.Name == "Copy floor");
            var loadedSourcePayload = Assert.IsType<SetGamePropertyPayload>(loadedSourceAction.Payload);
            Assert.Equal("legacy fallback", loadedSourcePayload.PropertyValue);
            Assert.Equal(room.Id, loadedSourcePayload.GamePropertyReference?.OwnerId);
            Assert.Equal("authoredDestination", loadedSourcePayload.GamePropertyReference?.PropertyName);
            Assert.Equal("currentRoom", loadedSourcePayload.PropertyValueSourceReference?.AnchorKey);
            Assert.Equal("floorNumber", loadedSourcePayload.PropertyValueSourceReference?.PropertyName);

            var loadedLiteralPayload = Assert.IsType<SetGamePropertyPayload>(loadedRoom.AvailableActions.Single(action => action.Name == "Legacy literal").Payload);
            Assert.Equal("still literal", loadedLiteralPayload.PropertyValue);
            Assert.Null(loadedLiteralPayload.PropertyValueSourceReference);

            service.ExportCleanProjectV1(projectPath, reopened);
            var runtimeRoomPath = Path.Combine(tempRoot, "GameRuntimeJson", "Room", room.Id.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtime = JsonDocument.Parse(File.ReadAllText(runtimeRoomPath));
            var runtimeActions = runtime.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();
            var runtimeSourcePayload = runtimeActions.Single(action => action.GetProperty("name").GetString() == "Copy floor").GetProperty("payload");
            Assert.Equal("floorNumber", runtimeSourcePayload.GetProperty("propertyValueSourceReference").GetProperty("propertyName").GetString());
            Assert.Equal("currentRoom", runtimeSourcePayload.GetProperty("propertyValueSourceReference").GetProperty("anchorKey").GetString());
            Assert.Equal(room.Id.ToString(), runtimeSourcePayload.GetProperty("gamePropertyReference").GetProperty("ownerId").GetString());
            var runtimeLiteralPayload = runtimeActions.Single(action => action.GetProperty("name").GetString() == "Legacy literal").GetProperty("payload");
            Assert.Equal("still literal", runtimeLiteralPayload.GetProperty("propertyValue").GetString());
            Assert.False(runtimeLiteralPayload.TryGetProperty("propertyValueSourceReference", out _));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static (ProjectModel Project, Room Room) BuildProjectWithRoom()
    {
        var room = new Room
        {
            Name = "Elevator Foyer",
            Variables = [new GamePropertyDefinition { Name = "authoredDestination", DefaultValue = "0" }]
        };
        var area = new Area { Name = "Area", Rooms = [room] };
        var project = new ProjectModel
        {
            Name = "Set Property Source",
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [area] }] }]
        };
        ScopeHierarchy.AttachParents(project);
        return (project, room);
    }
}
