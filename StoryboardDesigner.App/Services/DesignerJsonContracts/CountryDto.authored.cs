using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Services;

internal sealed partial class CountryDto : DesignerScopeNodeDtoBase
{
    public CountryDto()
    {
        ScopeKind = ScopeNodeKind.Country;
    }

    public Guid ParentPlanetId { get; set; }
    public List<string> BaseObjectsIgnoredValidationRuleIds { get; set; } = new();
    public List<ProjectGameObjectDto> BaseObjects { get; set; } = new();
    public List<ProjectGameObjectDto> GameObjects { get; set; } = new();
}
