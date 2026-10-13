using System.Threading;
using System.Windows.Controls;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalEditorDialogGuidedPairTests
{
    [Fact]
    public void GuidedEditor_ListsEverySouthNeighborAndInfersDirectionFromSelectedPair()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var large = new Room { Name = "Large" };
                var neighbors = Enumerable.Range(1, 3).Select(index => new Room { Name = $"South {index}" }).ToArray();
                var door = new GameObject { Name = "Elevator Door", IsOpenable = true, PositionX = 200, PositionY = 400 };
                large.GameObjects.Add(door);
                var area = new Area
                {
                    Name = "Area",
                    Rooms = [large, ..neighbors],
                    RoomPlacements =
                    [
                        new AreaRoomPlacement { RoomId = large.Id, X = 24, Y = 24, ColumnSpan = 3, RowSpan = 3 },
                        ..neighbors.Select((room, index) => new AreaRoomPlacement
                            { RoomId = room.Id, X = 24 + index * 180, Y = 444 })
                    ]
                };
                var connection = new TraversalConnection
                {
                    RoomAId = large.Id,
                    RoomBId = neighbors[1].Id,
                    BaseTraversalDirectionFromA = Direction10.South
                };
                var dialog = new TraversalEditorDialog(connection, area.Rooms, area);
                try
                {
                    var guided = (RadioButton)dialog.FindName("GuidedModeRadioButton")!;
                    var destination = (ComboBox)dialog.FindName("RoomBComboBox")!;
                    var direction = (ComboBox)dialog.FindName("DirectionComboBox")!;
                    var fromDoor = (ComboBox)dialog.FindName("FromRoomDoorComboBox")!;

                    Assert.True(guided.IsChecked);
                    Assert.Equal(3, destination.ItemsSource.Cast<Room>().Count());
                    Assert.Equal(neighbors[1].Id, destination.SelectedValue);
                    Assert.Equal(Direction10.South, direction.SelectedItem);
                    Assert.False(direction.IsEnabled);
                    Assert.Null(fromDoor.SelectedValue);
                    Assert.Equal("No door (open passage)",
                        fromDoor.SelectedItem!.GetType().GetProperty("Label")!.GetValue(fromDoor.SelectedItem));

                    destination.SelectedValue = neighbors[2].Id;
                    Assert.Equal(neighbors[2].Id, destination.SelectedValue);
                    Assert.Equal(Direction10.South, direction.SelectedItem);
                    Assert.Equal(door.ObjectId, fromDoor.ItemsSource.Cast<object>().Select(item =>
                        (Guid?)item.GetType().GetProperty("ObjectId")!.GetValue(item)).Single(id => id == door.ObjectId));
                    var doorLabel = fromDoor.ItemsSource.Cast<object>()
                        .Select(item => (string)item.GetType().GetProperty("Label")!.GetValue(item)!)
                        .Single(label => label.StartsWith("Elevator Door", StringComparison.Ordinal));
                    Assert.Contains("200, 400", doorLabel);
                    Assert.Contains(door.ObjectId.ToString("N"), doorLabel);
                }
                finally
                {
                    dialog.Close();
                }

                var newConnection = new TraversalConnection { RoomAId = large.Id };
                var addDialog = new TraversalEditorDialog(newConnection, area.Rooms, area);
                try
                {
                    var guided = (RadioButton)addDialog.FindName("GuidedModeRadioButton")!;
                    var destination = (ComboBox)addDialog.FindName("RoomBComboBox")!;
                    var direction = (ComboBox)addDialog.FindName("DirectionComboBox")!;
                    var fromDoor = (ComboBox)addDialog.FindName("FromRoomDoorComboBox")!;
                    var toDoor = (ComboBox)addDialog.FindName("ToRoomDoorComboBox")!;
                    Assert.True(guided.IsChecked);
                    Assert.Equal(3, destination.ItemsSource.Cast<Room>().Count());
                    Assert.Null(destination.SelectedValue);
                    destination.SelectedValue = neighbors[0].Id;
                    Assert.Equal(Direction10.South, direction.SelectedItem);
                    Assert.Null(fromDoor.SelectedValue);
                    Assert.Null(toDoor.SelectedValue);
                    Assert.Equal("No door (open passage)",
                        fromDoor.SelectedItem!.GetType().GetProperty("Label")!.GetValue(fromDoor.SelectedItem));
                    Assert.Equal("No door (open passage)",
                        toDoor.SelectedItem!.GetType().GetProperty("Label")!.GetValue(toDoor.SelectedItem));
                }
                finally
                {
                    addDialog.Close();
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        StaTestThreadRunner.StartAndWait(thread, nameof(GuidedEditor_ListsEverySouthNeighborAndInfersDirectionFromSelectedPair));
        if (failure is not null) throw failure;
    }
}
