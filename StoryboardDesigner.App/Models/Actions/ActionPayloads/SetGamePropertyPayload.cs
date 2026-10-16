using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public sealed record SetGamePropertyPayload(
    string PropertyName,
    string PropertyValue,
    ProjectGamePropertySourceReferenceDto? GamePropertyReference = null) : IActionPayload
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
