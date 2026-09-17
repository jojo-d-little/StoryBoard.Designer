using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class EventBindingConditionDefinition
{
    public RuntimeVariableQuantityEvaluationMode QuantityEvaluationMode { get; set; } = RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass;

    public List<EventBindingFilterConditionDefinition> Filters { get; set; } = new();
}
