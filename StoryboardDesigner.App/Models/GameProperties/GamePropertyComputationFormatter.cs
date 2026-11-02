using System.Globalization;
using System.Text.Json;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public static class GamePropertyComputationFormatter
{
    public static string Format(ProjectGamePropertyComputationDto? computation) => computation is null
        ? "(computation not defined)"
        : $"{computation.OutputType ?? "?"}: {Format(computation.Expression)}";

    public static string Format(ProjectGamePropertyExpressionDto? expression) => expression is null ? "<missing expression>" : expression.Kind switch
    {
        "literal" => FormatLiteral(expression.Value),
        "property" => FormatSourceReference(expression.Source),
        "exists" => "exists(" + FormatSourceReference(expression.Source) + ")",
        "compare" => $"({Format(expression.Left)} {expression.Operator ?? "?"} {Format(expression.Right)})",
        "and" or "or" => "(" + string.Join(expression.Kind == "and" ? " AND " : " OR ", (expression.Operands ?? []).Select(Format)) + ")",
        "not" => "NOT (" + Format(expression.Expression) + ")",
        "select" => string.Join("; ", (expression.Branches ?? []).Select((branch, index) =>
            $"{(index == 0 ? "IF" : "ELSE IF")} {Format(branch.When)} THEN {Format(branch.Value)}")) + $"; ELSE {Format(expression.Else)}",
        "add" => $"({Format(expression.Left)} + {Format(expression.Right)})",
        "subtract" => $"({Format(expression.Left)} - {Format(expression.Right)})",
        "multiply" => $"({Format(expression.Left)} * {Format(expression.Right)})",
        "divide" => $"({Format(expression.Left)} / {Format(expression.Right)})",
        _ => "<unknown expression>"
    };

    public static string FormatSourceReference(ProjectGamePropertySourceReferenceDto? source) => source is null
        ? "<choose property>"
        : (source.AnchorKey is not null ? source.AnchorKey + "." : source.OwnerId.HasValue ? $"owner[{source.OwnerId.Value:N}]." : "self.")
            + (source.PropertyName ?? "<missing>");

    private static string FormatLiteral(object? value) => value switch
    {
        null => "<missing literal>",
        string text => "\"" + text + "\"",
        bool boolean => boolean ? "true" : "false",
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        JsonElement element when element.ValueKind == JsonValueKind.String => "\"" + element.GetString() + "\"",
        JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetDouble().ToString("R", CultureInfo.InvariantCulture),
        JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean() ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<missing literal>"
    };
}
