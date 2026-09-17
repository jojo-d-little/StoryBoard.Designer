namespace StoryboardDesigner.App.Models;

public interface IActionPayload : IActionScriptFieldProvider, IActionLinkedActionsProvider, IActionReferenceTokenProvider
{
    CommandActionType ActionType { get; }
}
