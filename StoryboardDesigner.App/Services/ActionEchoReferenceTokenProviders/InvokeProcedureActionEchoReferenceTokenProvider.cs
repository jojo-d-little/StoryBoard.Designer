using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class InvokeProcedureActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public CommandActionType ActionType => CommandActionType.InvokeProcedure;

    public IReadOnlyList<string> GetTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }
}
