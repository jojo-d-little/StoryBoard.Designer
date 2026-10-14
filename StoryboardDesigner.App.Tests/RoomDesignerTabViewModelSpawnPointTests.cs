using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomDesignerTabViewModelSpawnPointTests
{
    [Fact]
    public void SpawnPointPlacement_UsesRoomCoordinatesWithoutCreatingPlayerObject()
    {
        var room = new Room { RoomImageCanvasWidth = 800, RoomImageCanvasHeight = 600 };
        var edits = 0;
        var tab = new RoomDesignerTabViewModel(room, 800, 600, 40, onEdited: () => edits++);
        tab.IsSnapToGridEnabled = false;
        tab.IsSpawnPointPlacementMode = true;

        Assert.True(tab.TryPlaceRoomSpawnPoint(123.5, 456.25));

        Assert.Equal(new RoomSpawnPoint(123.5, 456.25), room.SpawnPoint);
        Assert.Empty(room.GameObjects);
        Assert.False(tab.IsSpawnPointPlacementMode);
        Assert.Equal(1, edits);
    }

    [Fact]
    public void SpawnPointPlacement_SnapsAndClampsToRoomBounds()
    {
        var room = new Room();
        var tab = new RoomDesignerTabViewModel(room, 800, 600, 40);
        tab.IsSpawnPointPlacementMode = true;

        Assert.True(tab.TryPlaceRoomSpawnPoint(799, 599));

        Assert.Equal(new RoomSpawnPoint(800, 600), room.SpawnPoint);
    }
}
