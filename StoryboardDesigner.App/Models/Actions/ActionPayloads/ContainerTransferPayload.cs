namespace StoryboardDesigner.App.Models;

public sealed record ContainerTransferPayload(string TargetContainerId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.PutObjectInContainer;

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
