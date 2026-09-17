using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class RotateRoomObjectOnGridActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.RotateRoomObjectOnGrid;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
