using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class NavigateToAdjacentActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.NavigateToAdjacent;

    public IReadOnlyList<string> GetTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }
}
