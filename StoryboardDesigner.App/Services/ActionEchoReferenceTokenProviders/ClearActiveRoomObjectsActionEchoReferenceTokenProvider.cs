using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class ClearActiveRoomObjectsActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.ClearActiveRoomObjects;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
