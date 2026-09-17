using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomGameObjectsNodeViewModel : GameObjectsNodeViewModel
{
    public RoomGameObjectsNodeViewModel(RoomNodeViewModel roomNode)
        : base("Game Objects", roomNode)
    {
        RoomNode = roomNode;
    }

    public RoomNodeViewModel RoomNode { get; }
    public override List<GameObject> GameObjects => RoomNode.Room.GameObjects;
    public override IScopedAwareNode ScopeNode => RoomNode.Room;
}
