using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class TemplateRoomNodeViewModel : HierarchyNodeViewModel
{
    public TemplateRoomNodeViewModel(Room room, RoomTemplatesNodeViewModel parent)
        : base(room.Name, parent)
    {
        Room = room;
        ParentTemplatesNode = parent;
        NodeTypeLabel = "Template Room";
    }

    public Room Room { get; }
    public RoomTemplatesNodeViewModel ParentTemplatesNode { get; }

    protected override void RenameModel(string newName)
    {
        Room.Name = newName;
    }
}
