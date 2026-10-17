using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class SelectRoomObjectByPointActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.SelectRoomObjectByPoint;

    public IReadOnlyList<string> GetTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }
}
