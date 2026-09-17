using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomDesignerDirectionImageDialogServiceTests
{
    [Fact]
    public void OpenOrFocus_SameRoomAndDirection_ReusesExistingWindow()
    {
        var created = new List<FakeRoomDesignerDirectionImageDialogWindow>();
        var service = new RoomDesignerDirectionImageDialogService(_ =>
        {
            var window = new FakeRoomDesignerDirectionImageDialogWindow();
            created.Add(window);
            return window;
        });

        var slot = CreateSlot(RoomImageSlot.North);
        var roomId = Guid.NewGuid();

        service.OpenOrFocus(roomId, slot);
        service.OpenOrFocus(roomId, slot);

        Assert.Single(created);
        Assert.Equal(1, created[0].ShowCount);
        Assert.Equal(1, created[0].ActivateCount);
    }

    [Fact]
    public void OpenOrFocus_DifferentDirections_CreatesSeparateWindows()
    {
        var created = new List<FakeRoomDesignerDirectionImageDialogWindow>();
        var service = new RoomDesignerDirectionImageDialogService(_ =>
        {
            var window = new FakeRoomDesignerDirectionImageDialogWindow();
            created.Add(window);
            return window;
        });

        var roomId = Guid.NewGuid();

        service.OpenOrFocus(roomId, CreateSlot(RoomImageSlot.North));
        service.OpenOrFocus(roomId, CreateSlot(RoomImageSlot.East));

        Assert.Equal(2, created.Count);
        Assert.All(created, window => Assert.Equal(1, window.ShowCount));
    }

    [Fact]
    public void CloseAllEditors_ClosesEachOpenWindow()
    {
        var created = new List<FakeRoomDesignerDirectionImageDialogWindow>();
        var service = new RoomDesignerDirectionImageDialogService(_ =>
        {
            var window = new FakeRoomDesignerDirectionImageDialogWindow();
            created.Add(window);
            return window;
        });

        var roomId = Guid.NewGuid();
        service.OpenOrFocus(roomId, CreateSlot(RoomImageSlot.North));
        service.OpenOrFocus(roomId, CreateSlot(RoomImageSlot.East));

        service.CloseAllEditors();

        Assert.Equal(2, created.Count);
        Assert.All(created, window => Assert.Equal(1, window.CloseCount));
    }

    private static RoomDesignerImageSlotViewModel CreateSlot(RoomImageSlot slot)
    {
        var entry = new RoomImageEntry
        {
            Slot = slot,
            Image = new RoomImageVariant()
        };

        return new RoomDesignerImageSlotViewModel(entry);
    }

    private sealed class FakeRoomDesignerDirectionImageDialogWindow : IRoomDesignerDirectionImageDialogWindow
    {
        public event EventHandler? Closed;

        public int ShowCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int CloseCount { get; private set; }

        public void Show()
        {
            ShowCount++;
        }

        public void Activate()
        {
            ActivateCount++;
        }

        public void Close()
        {
            CloseCount++;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
