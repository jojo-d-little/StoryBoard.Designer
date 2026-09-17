namespace StoryboardDesigner.App.Models;

public enum EventInputArgumentMappingSourceType
{
    EventPayloadKey,
    ConstantValue
}

public sealed class EventInputArgumentMappingDefinition
{
    public string InputEventPaylloadArgKey { get; set; } = string.Empty;

    public string OutputActionPayloadArgKey { get; set; } = string.Empty;

    public EventInputArgumentMappingSourceType SourceType { get; set; } = EventInputArgumentMappingSourceType.EventPayloadKey;

    public string ConstantValue { get; set; } = string.Empty;

    public static EventInputArgumentMappingDefinition FromPersisted(string? inputEventPayloadArgKey, string? outputActionPayloadArgKey)
    {
        var normalizedInput = inputEventPayloadArgKey?.Trim() ?? string.Empty;
        var normalizedOutput = outputActionPayloadArgKey?.Trim() ?? string.Empty;

        if (Storyboard.Shared.RuntimeContracts.RuntimeEventInputArgumentMappingConventions.TryDecodeConstantTargetKey(
                normalizedInput,
                out var constantTargetOutputArgKey))
        {
            return new EventInputArgumentMappingDefinition
            {
                SourceType = EventInputArgumentMappingSourceType.ConstantValue,
                InputEventPaylloadArgKey = string.Empty,
                OutputActionPayloadArgKey = constantTargetOutputArgKey,
                ConstantValue = normalizedOutput
            };
        }

        return new EventInputArgumentMappingDefinition
        {
            SourceType = EventInputArgumentMappingSourceType.EventPayloadKey,
            InputEventPaylloadArgKey = normalizedInput,
            OutputActionPayloadArgKey = normalizedOutput,
            ConstantValue = string.Empty
        };
    }

    public void ToPersisted(out string inputEventPayloadArgKey, out string? outputActionPayloadArgKey)
    {
        if (SourceType == EventInputArgumentMappingSourceType.ConstantValue)
        {
            inputEventPayloadArgKey = Storyboard.Shared.RuntimeContracts.RuntimeEventInputArgumentMappingConventions
                .EncodeConstantTargetKey(OutputActionPayloadArgKey);
            outputActionPayloadArgKey = string.IsNullOrWhiteSpace(ConstantValue)
                ? null
                : ConstantValue.Trim();
            return;
        }

        inputEventPayloadArgKey = InputEventPaylloadArgKey?.Trim() ?? string.Empty;
        outputActionPayloadArgKey = string.IsNullOrWhiteSpace(OutputActionPayloadArgKey)
            ? null
            : OutputActionPayloadArgKey.Trim();
    }

    public EventInputArgumentMappingDefinition CloneNormalized()
    {
        return new EventInputArgumentMappingDefinition
        {
            SourceType = SourceType,
            InputEventPaylloadArgKey = InputEventPaylloadArgKey?.Trim() ?? string.Empty,
            OutputActionPayloadArgKey = OutputActionPayloadArgKey?.Trim() ?? string.Empty,
            ConstantValue = ConstantValue?.Trim() ?? string.Empty
        };
    }
}
