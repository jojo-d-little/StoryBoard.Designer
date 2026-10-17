using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class SetActiveRoomObjectActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.SetActiveRoomObject;

    public IReadOnlyList<string> GetTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }
}
