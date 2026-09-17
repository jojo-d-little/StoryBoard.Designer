using Storyboard.Shared.RuntimeContracts.Dtos;

namespace StoryboardDesigner.App.Services;

internal sealed partial class ProjectGlobalNodeDto
{
    public ProjectGlobalNodeDto()
    {
        ScopeKind = ScopeNodeKind.Global;
    }

    public List<ProjectGameObjectDto>? GameObjects { get; set; }
    public List<ProjectGameObjectDto>? BaseObjects { get; set; }
    public List<PhaseNodeDto>? PhaseBooks { get; set; }
}