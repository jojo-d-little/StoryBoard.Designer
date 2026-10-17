using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Models;

public sealed record NavigatePayload : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.NavigateDirection;

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
