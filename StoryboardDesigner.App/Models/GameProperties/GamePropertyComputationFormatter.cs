using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public static class GamePropertyComputationFormatter
{
    public static string Format(ProjectGamePropertyConditionDto? condition) => condition is null
        ? "(computation not defined)"
        : FormatCondition(condition);

    private static string FormatCondition(ProjectGamePropertyConditionDto condition) => condition.Kind switch
    {
        "All" => "(" + string.Join(" AND ", (condition.Conditions ?? new()).Select(FormatCondition)) + ")",
        "Any" => "(" + string.Join(" OR ", (condition.Conditions ?? new()).Select(FormatCondition)) + ")",
        "Not" => "NOT (" + (condition.Condition is null ? "<missing>" : FormatCondition(condition.Condition)) + ")",
        "Exists" => "exists(" + FormatSourceReference(condition.GamePropertySource) + ")",
        "Compare" => FormatSourceReference(condition.GamePropertySource)
            + " " + (condition.Operator ?? "?")
            + (condition.RightOperand is null
                ? string.Empty
                : " " + (condition.RightOperand.GamePropertySource is not null
                    ? FormatSourceReference(condition.RightOperand.GamePropertySource)
                    : condition.RightOperand.Value ?? string.Empty)),
        _ => "<unknown condition>"
    };

    public static string FormatSourceReference(ProjectGamePropertySourceReferenceDto? source) => source is null
        ? "<choose property>"
        : (source.AnchorKey is not null ? source.AnchorKey + "." : source.OwnerId.HasValue ? $"owner[{source.OwnerId.Value:N}]." : "self.")
            + (source.PropertyName ?? "<missing>");
}
