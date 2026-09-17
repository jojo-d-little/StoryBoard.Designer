using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class MoveRoomObjectOnGridActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.MoveRoomObjectOnGrid;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
