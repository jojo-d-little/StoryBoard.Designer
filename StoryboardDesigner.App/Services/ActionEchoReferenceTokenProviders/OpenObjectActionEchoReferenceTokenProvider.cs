using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class OpenObjectActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.OpenObject;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
