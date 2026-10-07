using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public static class CommandContextPayloadValidator
{
    public static bool TryValidate(
        CommandContextField field,
        bool? expectedBoolean,
        Direction10? expectedDirection,
        out string message)
    {
        message = field switch
        {
            CommandContextField.DirectionSpecified or CommandContextField.DestinationRoomQualifierResolved
                when expectedBoolean.HasValue && expectedDirection is null => string.Empty,
            CommandContextField.Direction
                when expectedBoolean is null && expectedDirection.HasValue && Enum.IsDefined(expectedDirection.Value) => string.Empty,
            CommandContextField.DirectionSpecified or CommandContextField.DestinationRoomQualifierResolved
                => "This command detail requires a True/False value and no direction value.",
            CommandContextField.Direction
                => "Normalized direction requires a known direction value and no boolean value.",
            _ => "Choose a supported command detail."
        };

        return string.IsNullOrEmpty(message);
    }
}
