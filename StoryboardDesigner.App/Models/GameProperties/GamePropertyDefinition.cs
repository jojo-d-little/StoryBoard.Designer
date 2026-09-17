namespace StoryboardDesigner.App.Models;

public sealed class GamePropertyDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "newVariable";
    public string DefaultValue { get; set; } = "false";
    public GamePropertyValueRestriction ValueRestriction { get; set; } = GamePropertyValueRestriction.TrueFalse;
    public GamePropertyLifetime Lifetime { get; set; } = GamePropertyLifetime.Singleton;
    public Guid? SharedVariableId { get; set; }
}
