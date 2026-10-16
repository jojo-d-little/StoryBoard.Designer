using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public sealed record CheckGamePropertyPayload(
    string PropertyName,
    bool ExpectedValue,
    ProjectGamePropertySourceReferenceDto? GamePropertyReference = null) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.CheckGameProperty;
}
