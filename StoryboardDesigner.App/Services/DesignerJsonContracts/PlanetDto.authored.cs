using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Services;

internal sealed partial class PlanetDto : DesignerScopeNodeDtoBase
{
    public PlanetDto()
    {
        ScopeKind = ScopeNodeKind.Planet;
    }

    public List<string> BaseObjectsIgnoredValidationRuleIds { get; set; } = new();
    public List<ProjectGameObjectDto> BaseObjects { get; set; } = new();
    public List<ProjectGameObjectDto> GameObjects { get; set; } = new();
}
