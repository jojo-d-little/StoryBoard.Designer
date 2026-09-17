using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Services;

internal sealed partial class AreaDto : DesignerScopeNodeDtoBase
{
    public AreaDto()
    {
        ScopeKind = ScopeNodeKind.Area;
    }

    public Guid ParentCountryId { get; set; }
    public AreaRoomDropBehavior RoomDropBehavior { get; set; } = AreaRoomDropBehavior.KeepDisconnected;
    public string? TraversalModeOverride { get; set; }
    public List<string> BaseObjectsIgnoredValidationRuleIds { get; set; } = new();
    public List<ProjectGameObjectDto> BaseObjects { get; set; } = new();
    public List<ProjectGameObjectDto> GameObjects { get; set; } = new();
}
