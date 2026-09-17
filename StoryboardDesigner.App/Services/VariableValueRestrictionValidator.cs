using System.Globalization;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class VariableValueRestrictionValidator
{
    public static bool IsAllowed(GamePropertyValueRestriction restriction, string? value)
    {
        var candidate = value?.Trim() ?? string.Empty;

        return restriction switch
        {
            GamePropertyValueRestriction.Unrestricted => true,
            GamePropertyValueRestriction.Numeric => IsNumeric(candidate),
            GamePropertyValueRestriction.TrueFalse => IsBoolean(candidate),
            _ => true
        };
    }

    public static string BuildInvalidValueMessage(GamePropertyValueRestriction restriction, string valueSourceLabel)
    {
        return restriction switch
        {
            GamePropertyValueRestriction.Numeric => $"{valueSourceLabel} must be numeric for variables with the Numeric restriction.",
            GamePropertyValueRestriction.TrueFalse => $"{valueSourceLabel} must be 'true' or 'false' for variables with the True False restriction.",
            _ => $"{valueSourceLabel} is not valid for the selected restriction."
        };
    }

    private static bool IsNumeric(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
               || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out _);
    }

    private static bool IsBoolean(string value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }
}
