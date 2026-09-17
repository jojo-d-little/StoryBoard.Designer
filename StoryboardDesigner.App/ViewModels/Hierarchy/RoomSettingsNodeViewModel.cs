using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomSettingsNodeViewModel : HierarchyNodeViewModel
{
    public RoomSettingsNodeViewModel(RoomNodeViewModel roomNode)
        : base("Room Settings", roomNode)
    {
        RoomNode = roomNode;
        TemplateRoomNode = null;
        Room = roomNode.Room;
        NodeTypeLabel = "Room Settings";
    }

    public RoomSettingsNodeViewModel(TemplateRoomNodeViewModel templateRoomNode)
        : base("Room Settings", templateRoomNode)
    {
        RoomNode = null;
        TemplateRoomNode = templateRoomNode;
        Room = templateRoomNode.Room;
        NodeTypeLabel = "Room Settings";
    }

    public RoomNodeViewModel? RoomNode { get; }
    public TemplateRoomNodeViewModel? TemplateRoomNode { get; }
    public Room Room { get; }

    protected override void RenameModel(string newName)
    {
    }
}
