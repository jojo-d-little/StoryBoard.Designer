using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class StackRoomObjectOnAnotherActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.StackRoomObjectOnAnother;

    public IReadOnlyList<string> GetTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }
}
