using System.Text.Json.Serialization;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Adds optional per-binding input argument mappings for event action bindings.
/// </summary>
internal sealed partial class ProjectEventActionBindingDto
{
    /// <summary>
    /// Gets or sets optional input argument mappings scoped to this action binding.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectEventInputArgumentMappingDto>? InputArgumentMappings { get; set; }
}
