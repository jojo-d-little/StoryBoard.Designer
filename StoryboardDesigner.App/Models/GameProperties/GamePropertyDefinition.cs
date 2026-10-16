using System.Text.Json.Serialization;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public sealed class GamePropertyDefinition
{
    [JsonIgnore]
    public bool IsGeneratedEnvironmentalProperty { get; set; }
    [JsonIgnore]
    public string? GeneratedPreviewValue { get; set; }
    [JsonIgnore]
    public string ComputationSummary => Computation is null
        ? string.Empty
        : GamePropertyComputationFormatter.Format(Computation.Condition);
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "newVariable";
    public string DefaultValue { get; set; } = "false";
    public GamePropertyValueRestriction ValueRestriction { get; set; } = GamePropertyValueRestriction.TrueFalse;
    public GamePropertyLifetime Lifetime { get; set; } = GamePropertyLifetime.Singleton;
    public Guid? SharedVariableId { get; set; }
    public ProjectGamePropertyComputationDto? Computation { get; set; }
}
