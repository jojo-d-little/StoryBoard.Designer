using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class BuildCompositeByTargetActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.BuildCompositeByTarget;

    public IReadOnlyList<string> GetTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }
}
