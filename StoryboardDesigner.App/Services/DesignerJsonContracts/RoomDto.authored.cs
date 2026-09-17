namespace StoryboardDesigner.App.Services;

internal sealed partial class RoomDto
{
    public RoomDto()
    {
        ScopeKind = ScopeNodeKind.Room;
    }

    // Transitional compatibility payload for embedded room objects.
    // Canonical persisted relationship is GameObjectIds in RoomDto.sbe.contract.cs.
    public List<ProjectGameObjectDto> GameObjects { get; set; } = new();
}
