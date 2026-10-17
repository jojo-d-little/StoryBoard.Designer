using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class BuildCompositeByPartsActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.BuildCompositeByParts;

    public IReadOnlyList<string> GetTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }
}
