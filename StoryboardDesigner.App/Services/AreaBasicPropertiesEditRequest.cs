using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record AreaBasicPropertiesEditRequest(
    string Name,
    string ProducerNotes,
    AreaAdjacencyMode AdjacencyMode,
    AreaRoomDropBehavior RoomDropBehavior,
    Guid? StartingRoomId);
