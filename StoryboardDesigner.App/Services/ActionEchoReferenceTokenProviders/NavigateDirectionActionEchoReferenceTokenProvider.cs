using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class NavigateDirectionActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.NavigateDirection;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
