using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class PutObjectInContainerActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.PutObjectInContainer;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
