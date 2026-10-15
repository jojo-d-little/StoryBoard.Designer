using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomDesignerTabViewModelSpawnPointTests
{
    [Fact]
    public void SpawnPointDrag_UsesRoomCoordinatesWithoutCreatingPlayerObject()
    {
        var room = new Room { RoomImageCanvasWidth = 800, RoomImageCanvasHeight = 600 };
        room.SetDefaultSpawnPoint(new RoomSpawnPoint(400, 300, IsDefault: true));
        var edits = 0;
        var tab = new RoomDesignerTabViewModel(room, 800, 600, 40, onEdited: () => edits++);
        tab.IsSnapToGridEnabled = false;
        var marker = tab.RoomRenderOrderItems.Single(item => item.IsSpawnPoint);
        Assert.True(tab.TryMoveRoomSpawnPoint(marker, 123.5, 456.25));

        Assert.Equal((123.5, 456.25), (room.SpawnPoint.X, room.SpawnPoint.Y));
        Assert.Empty(room.GameObjects);
        Assert.Equal(1, edits);
    }

    [Fact]
    public void SpawnPointDrag_SnapsAndClampsToRoomBounds()
    {
        var room = new Room();
        room.SetDefaultSpawnPoint(new RoomSpawnPoint(400, 300, IsDefault: true));
        var tab = new RoomDesignerTabViewModel(room, 800, 600, 40);
        var marker = tab.RoomRenderOrderItems.Single(item => item.IsSpawnPoint);
        Assert.True(tab.TryMoveRoomSpawnPoint(marker, 799, 599));

        Assert.Equal((800, 600), (room.SpawnPoint.X, room.SpawnPoint.Y));
    }

    [Fact]
    public void TraversalSpawnPointCanBeSelectedMovedInZOrderAndGivenFacingOverride()
    {
        var traversalId = Guid.NewGuid();
        var room = new Room
        {
            SpawnPoints =
            [
                new RoomSpawnPoint(400, 300, 5, IsDefault: true),
                new RoomSpawnPoint(36, 300, 4, IsDefault: false, TraversalConnectionId: traversalId)
            ]
        };
        var area = new Area { Rooms = [room] };
        var tab = new RoomDesignerTabViewModel(room, 800, 600, area: area);
        var arrival = tab.RoomRenderOrderItems.Single(item => item.SpawnPoint?.TraversalConnectionId == traversalId);
        tab.SelectedRoomRenderOrderItem = arrival;
        tab.SelectedSpawnPointFacingOverride = Direction10.North;
        var refreshedArrival = tab.RoomRenderOrderItems.Single(item => item.SpawnPoint?.TraversalConnectionId == traversalId);
        tab.MoveRoomChildObjectUpCommand.Execute(refreshedArrival);

        var savedArrival = Assert.Single(room.SpawnPoints, point => point.TraversalConnectionId == traversalId);
        Assert.Equal(Direction10.North, savedArrival.FacingOverride);
        Assert.Equal(2, savedArrival.RenderZOrder);
        Assert.Equal(1, room.SpawnPoint.RenderZOrder);
    }

    [Fact]
    public void DefaultSpawnPointIsOptInAndCanBeTurnedOff()
    {
        var room = new Room();
        var tab = new RoomDesignerTabViewModel(room, 800, 600);

        Assert.Empty(room.SpawnPoints);
        Assert.False(tab.HasDefaultSpawnPoint);

        tab.ToggleDefaultSpawnPointCommand.Execute(null);
        Assert.True(tab.HasDefaultSpawnPoint);
        Assert.Single(room.SpawnPoints, point => point.IsDefault);

        tab.ToggleDefaultSpawnPointCommand.Execute(null);
        Assert.False(tab.HasDefaultSpawnPoint);
        Assert.Empty(room.SpawnPoints);
    }
}
