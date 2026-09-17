namespace StoryboardDesigner.App.Models;

public sealed class TraversalLegState
{
    public Guid? OpenableObjectId { get; set; }
    public OpenablePolicy OpenStatePolicy { get; set; } = OpenablePolicy.IgnoreOpenableState;
    public Guid? SharedVariableId { get; set; }
    public List<CommandAction> AvailableActions { get; set; } = new();
    public List<GamePropertyDefinition> Variables { get; set; } =
    [
        new()
        {
            Name = "isPassable",
            DefaultValue = "true",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Lifetime = GamePropertyLifetime.Singleton
        }
    ];
}
