using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomNodeViewModel : HierarchyNodeViewModel
{
    public RoomNodeViewModel(Room room, Area area, HierarchyNodeViewModel parent)
        : base(room.Name, parent)
    {
        Room = room;
        Area = area;
        NodeTypeLabel = "Room";
    }

    public Room Room { get; }
    public Area Area { get; }
    public bool IsStartingRoom => Area.StartingRoomId.HasValue && Area.StartingRoomId.Value == Room.Id;

    public void RefreshStartIndicator()
    {
        OnPropertyChanged(nameof(IsStartingRoom));
    }

    protected override void RenameModel(string newName)
    {
        Room.Name = newName;
    }
}
