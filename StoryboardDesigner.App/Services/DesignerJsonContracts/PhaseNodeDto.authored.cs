using System.Text.Json.Serialization;

namespace StoryboardDesigner.App.Services;

internal sealed partial class PhaseNodeDto
{
    public PhaseNodeDto()
    {
        Id = Guid.NewGuid();
        ScopeKind = ScopeNodeKind.Page;
        PhaseKey = string.Empty;
        DisplayName = string.Empty;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PhaseNodeDto>? Phases { get; set; }
}
