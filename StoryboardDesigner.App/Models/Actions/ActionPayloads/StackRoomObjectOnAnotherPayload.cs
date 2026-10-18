namespace StoryboardDesigner.App.Models;

public sealed record StackRoomObjectOnAnotherPayload : IActionPayload
{
    public StackRoomObjectOnAnotherPayload(
        string movementEffectKey = MovementPresentationEffectKeys.DefaultMedium)
    {
        MovementEffectKey = movementEffectKey?.Trim() ?? string.Empty;
    }

    public CommandActionType ActionType => CommandActionType.StackRoomObjectOnAnother;

    public string MovementEffectKey { get; init; }

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
