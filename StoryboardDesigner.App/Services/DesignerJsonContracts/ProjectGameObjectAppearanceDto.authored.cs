using System.Text.Json.Serialization;

namespace StoryboardDesigner.App.Services;

internal sealed partial class ProjectGameObjectAppearanceDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("stackOrder")]
    public int? LegacyStackOrder
    {
        get => null;
        set
        {
            if (value.HasValue && StackGroup == 0)
            {
                StackGroup = value.Value;
            }
        }
    }
}
