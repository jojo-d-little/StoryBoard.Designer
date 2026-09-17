using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameServices.References;
using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Models;

public sealed record StackRoomObjectOnAnotherPayload : IActionPayload
{
    public StackRoomObjectOnAnotherPayload(
        RuntimeMovementVisualTransitionHint visualTransitionHint = RuntimeMovementVisualTransitionHint.Medium)
    {
        VisualTransitionHint = visualTransitionHint;
    }

    public CommandActionType ActionType => CommandActionType.StackRoomObjectOnAnother;

    public RuntimeMovementVisualTransitionHint VisualTransitionHint { get; init; }

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
