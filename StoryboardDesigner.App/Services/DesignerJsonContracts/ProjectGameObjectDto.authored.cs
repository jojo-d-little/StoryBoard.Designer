using System.Text.Json.Serialization;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Services;

internal sealed partial class ProjectGameObjectDto : DesignerScopeNodeDtoBase
{
    public ProjectGameObjectDto()
    {
        Id = Guid.NewGuid();
        ScopeKind = ScopeNodeKind.GameObject;
    }

    // Compatibility read alias for legacy payloads that used objectId for identity.
    [JsonPropertyName("objectId")]
    public Guid? LegacyObjectId
    {
        set
        {
            if (!value.HasValue || value.Value == Guid.Empty)
            {
                return;
            }

            Id = value.Value;
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ObjectType { get; set; }

    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public bool IncludeInPreview { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string?>? InstanceOverrides { get; set; }
}
