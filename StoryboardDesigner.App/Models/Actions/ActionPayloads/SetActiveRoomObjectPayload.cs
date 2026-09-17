using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Models;

public sealed record SetActiveRoomObjectPayload(string SelectionCueEffectKey) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.SetActiveRoomObject;

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
