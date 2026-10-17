using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class BreakCompositeItemActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.BreakCompositeItem;

    public IReadOnlyList<string> GetTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }
}
