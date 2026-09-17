namespace StoryboardDesigner.App.Models;

public sealed class EventActionBindingDefinition
{
    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;

    public EventBindingConditionDefinition Condition { get; set; } = new();

    public EventBindingTargetDefinition Target { get; set; } = new();

    public List<EventInputArgumentMappingDefinition> InputArgumentMappings { get; set; } = new();
}
