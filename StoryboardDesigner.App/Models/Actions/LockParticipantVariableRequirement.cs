using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class LockParticipantVariableRequirement
{
    public string VariableName { get; set; } = string.Empty;
    public RuntimeVariableComparisonOperator Operator { get; set; } = RuntimeVariableComparisonOperator.Equals;
    public string? ExpectedValue { get; set; }
    public RuntimeVariableQuantityEvaluationMode QuantityEvaluationMode { get; set; } = RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass;
}
