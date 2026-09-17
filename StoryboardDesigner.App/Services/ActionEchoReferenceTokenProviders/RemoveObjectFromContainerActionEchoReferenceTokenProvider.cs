using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class RemoveObjectFromContainerActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.RemoveObjectFromContainer;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
