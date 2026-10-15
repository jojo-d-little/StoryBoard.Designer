using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomDesignerTabViewModelSpawnPointTests
{
    [Fact]
    public void SpawnPointDrag_UsesRoomCoordinatesWithoutCreatingPlayerObject()
    {
        var room = new Room { RoomImageCanvasWidth = 800, RoomImageCanvasHeight = 600 };
        var edits = 0;
        var tab = new RoomDesignerTabViewModel(room, 800, 600, 40, onEdited: () => edits++);
        tab.IsSnapToGridEnabled = false;
        Assert.True(tab.TryMoveRoomSpawnPoint(123.5, 456.25));

        Assert.Equal(new RoomSpawnPoint(123.5, 456.25), room.SpawnPoint);
        Assert.Empty(room.GameObjects);
        Assert.Equal(1, edits);
    }

    [Fact]
    public void SpawnPointDrag_SnapsAndClampsToRoomBounds()
    {
        var room = new Room();
        var tab = new RoomDesignerTabViewModel(room, 800, 600, 40);
        Assert.True(tab.TryMoveRoomSpawnPoint(799, 599));

        Assert.Equal(new RoomSpawnPoint(800, 600), room.SpawnPoint);
    }
}
