namespace StoryboardDesigner.App.Models;

public sealed record EchoPayload : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.EchoMessage;

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
