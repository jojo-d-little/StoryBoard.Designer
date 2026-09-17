namespace StoryboardDesigner.App.Models;

public sealed record SetGamePropertyPayload(string PropertyName, string PropertyValue) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.SetGameProperty;

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return
        [
            (nameof(CommandAction.GamePropertyValue), PropertyValue ?? string.Empty)
        ];
    }
}
