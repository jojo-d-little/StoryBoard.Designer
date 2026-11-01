using System.Globalization;
using System.Text.Json;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public static class GamePropertyArithmeticAnalysis
{
    public static IEnumerable<string> ValidateNumericConversion(ProjectGamePropertyExpressionDto? expression)
    {
        if (expression is null) yield break;

        if (expression.Kind == "literal" && IsInvalidNumericString(expression.Value))
        {
            yield return "A String literal used by arithmetic must be blank or parse as a finite invariant-culture number.";
            yield break;
        }

        if (expression.Kind != "select") yield break;
        foreach (var branch in expression.Branches ?? [])
            foreach (var issue in ValidateNumericConversion(branch.Value))
                yield return issue;
        foreach (var issue in ValidateNumericConversion(expression.Else))
            yield return issue;
    }

    public static IEnumerable<string> DivisionByZeroWarnings(ProjectGamePropertyExpressionDto? expression)
    {
        if (expression is null) yield break;
        if (expression.Kind == "divide" && TryGetConstantNumericValue(expression.Right, out var divisor) && divisor == 0d)
            yield return "A constant zero divisor will produce Numeric 0 at runtime.";

        foreach (var child in Children(expression))
            foreach (var warning in DivisionByZeroWarnings(child))
                yield return warning;
    }

    public static bool TryGetConstantNumericValue(ProjectGamePropertyExpressionDto? expression, out double value)
    {
        value = 0d;
        if (expression is null) return false;
        if (expression.Kind == "literal") return TryConvertLiteralToNumeric(expression.Value, out value);
        if (expression.Kind is not ("add" or "subtract" or "multiply" or "divide")
            || !TryGetConstantNumericValue(expression.Left, out var left)
            || !TryGetConstantNumericValue(expression.Right, out var right))
            return false;

        value = expression.Kind switch
        {
            "add" => left + right,
            "subtract" => left - right,
            "multiply" => left * right,
            "divide" when right == 0d => 0d,
            "divide" => left / right,
            _ => double.NaN
        };
        return double.IsFinite(value);
    }

    public static bool HasNonFiniteConstantArithmeticResult(ProjectGamePropertyExpressionDto? expression)
    {
        if (expression?.Kind is not ("add" or "subtract" or "multiply" or "divide")
            || !TryGetConstantNumericValue(expression.Left, out var left)
            || !TryGetConstantNumericValue(expression.Right, out var right))
            return false;

        var result = expression.Kind switch
        {
            "add" => left + right,
            "subtract" => left - right,
            "multiply" => left * right,
            "divide" when right == 0d => 0d,
            "divide" => left / right,
            _ => double.NaN
        };
        return !double.IsFinite(result);
    }

    private static bool IsInvalidNumericString(object? value) => TryGetStringValue(value, out var text)
        && !TryParseFiniteNumericString(text, out _);

    private static bool TryConvertLiteralToNumeric(object? value, out double number)
    {
        number = 0d;
        switch (value)
        {
            case bool boolean:
                number = boolean ? 1d : 0d;
                return true;
            case string text:
                return TryParseFiniteNumericString(text, out number);
            case JsonElement element when element.ValueKind == JsonValueKind.True:
                number = 1d;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.False:
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.String:
                return TryParseFiniteNumericString(element.GetString() ?? string.Empty, out number);
            case JsonElement element when element.ValueKind == JsonValueKind.Number
                && element.TryGetDouble(out number) && double.IsFinite(number):
                return true;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                try
                {
                    number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return double.IsFinite(number);
                }
                catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
                {
                    return false;
                }
            default:
                return false;
        }
    }

    private static bool TryGetStringValue(object? value, out string text)
    {
        if (value is string stringValue)
        {
            text = stringValue;
            return true;
        }
        if (value is JsonElement { ValueKind: JsonValueKind.String } element)
        {
            text = element.GetString() ?? string.Empty;
            return true;
        }
        text = string.Empty;
        return false;
    }

    private static bool TryParseFiniteNumericString(string text, out double number)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            number = 0d;
            return true;
        }
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && double.IsFinite(number);
    }

    private static IEnumerable<ProjectGamePropertyExpressionDto?> Children(ProjectGamePropertyExpressionDto expression)
    {
        if (expression.Left is not null) yield return expression.Left;
        if (expression.Right is not null) yield return expression.Right;
        if (expression.Expression is not null) yield return expression.Expression;
        foreach (var operand in expression.Operands ?? []) yield return operand;
        foreach (var branch in expression.Branches ?? [])
        {
            if (branch.When is not null) yield return branch.When;
            if (branch.Value is not null) yield return branch.Value;
        }
        if (expression.Else is not null) yield return expression.Else;
    }
}
