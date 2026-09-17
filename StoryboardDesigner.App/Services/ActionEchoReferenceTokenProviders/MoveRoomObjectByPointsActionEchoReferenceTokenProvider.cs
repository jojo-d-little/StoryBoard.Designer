using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class MoveRoomObjectByPointsActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.MoveRoomObjectByPoints;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
