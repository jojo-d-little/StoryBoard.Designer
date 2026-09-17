using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class EventBindingFilterConditionDefinition
{
    public string VariableName { get; set; } = string.Empty;

    public RuntimeVariableComparisonOperator Operator { get; set; } = RuntimeVariableComparisonOperator.Equals;

    public string? ExpectedValue { get; set; }
}
