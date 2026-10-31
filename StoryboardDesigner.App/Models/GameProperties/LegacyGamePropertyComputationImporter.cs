using System.Globalization;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

/// <summary>Converts legacy computation payloads into the unified V2 expression tree for migration editing.</summary>
public static class LegacyGamePropertyComputationImporter
{
    public static ProjectGamePropertyComputationV2Dto? Convert(ProjectGamePropertyComputationDto? computation)
    {
        if (computation is null) return null;
        var resultKind = computation.ResultKind ?? "Boolean";
        var root = string.Equals(resultKind, "Boolean", StringComparison.OrdinalIgnoreCase)
            ? ConvertCondition(computation.Condition) ?? Literal(false)
            : new ProjectGamePropertyExpressionDto
            {
                Kind = "select",
                Branches = (computation.ResultBranches ?? []).Select(branch => new ProjectGamePropertySelectBranchDto
                {
                    When = ConvertCondition(branch.Condition) ?? Literal(false),
                    Value = ConvertOperand(branch.Result) ?? Literal(NeutralValue(resultKind))
                }).ToList(),
                Else = ConvertOperand(computation.ElseResult) ?? Literal(NeutralValue(resultKind))
            };
        return root is null ? null : new ProjectGamePropertyComputationV2Dto { Expression = root, OutputType = resultKind };
    }

    private static ProjectGamePropertyExpressionDto? ConvertCondition(ProjectGamePropertyConditionDto? condition)
    {
        if (condition is null) return null;
        return condition.Kind switch
        {
            "All" => new ProjectGamePropertyExpressionDto { Kind = "and", Operands = (condition.Conditions ?? []).Select(ConvertCondition).Where(static item => item is not null).Cast<ProjectGamePropertyExpressionDto>().ToList() },
            "Any" => new ProjectGamePropertyExpressionDto { Kind = "or", Operands = (condition.Conditions ?? []).Select(ConvertCondition).Where(static item => item is not null).Cast<ProjectGamePropertyExpressionDto>().ToList() },
            "Not" => new ProjectGamePropertyExpressionDto { Kind = "not", Expression = ConvertCondition(condition.Condition) ?? Literal(false) },
            "Exists" => new ProjectGamePropertyExpressionDto { Kind = "exists", Source = Clone(condition.GamePropertySource) ?? MissingSource() },
            "Compare" when condition.Operator is "IsTrue" or "IsFalse" => new ProjectGamePropertyExpressionDto
            {
                Kind = "compare", Operator = "Equals",
                Left = new ProjectGamePropertyExpressionDto { Kind = "property", Source = Clone(condition.GamePropertySource) ?? MissingSource() },
                Right = new ProjectGamePropertyExpressionDto { Kind = "literal", Value = condition.Operator == "IsTrue" }
            },
            "Compare" => new ProjectGamePropertyExpressionDto
            {
                Kind = "compare", Operator = condition.Operator ?? "Equals",
                Left = new ProjectGamePropertyExpressionDto { Kind = "property", Source = Clone(condition.GamePropertySource) ?? MissingSource() },
                Right = ConvertOperand(condition.RightOperand) ?? Literal(string.Empty)
            },
            _ => null
        };
    }

    private static ProjectGamePropertyExpressionDto? ConvertOperand(ProjectGamePropertyOperandDto? operand)
    {
        if (operand is null) return null;
        if (operand.GamePropertySource is not null)
            return new ProjectGamePropertyExpressionDto { Kind = "property", Source = Clone(operand.GamePropertySource) ?? MissingSource() };
        object? value = operand.ValueKind?.ToLowerInvariant() switch
        {
            "boolean" when bool.TryParse(operand.Value, out var boolean) => boolean,
            "numeric" when double.TryParse(operand.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
            _ => operand.Value ?? string.Empty
        };
        return new ProjectGamePropertyExpressionDto { Kind = "literal", Value = value };
    }

    private static ProjectGamePropertySourceReferenceDto? Clone(ProjectGamePropertySourceReferenceDto? source) => source is null
        ? null
        : new ProjectGamePropertySourceReferenceDto { AnchorKey = source.AnchorKey, OwnerId = source.OwnerId, PropertyName = source.PropertyName };

    private static ProjectGamePropertySourceReferenceDto MissingSource() => new() { PropertyName = string.Empty };
    private static ProjectGamePropertyExpressionDto Literal(object value) => new() { Kind = "literal", Value = value };
    private static object NeutralValue(string kind) => kind switch { "Numeric" => 0d, "String" => string.Empty, _ => false };
}
