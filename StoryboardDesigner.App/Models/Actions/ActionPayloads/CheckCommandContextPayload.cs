using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed record CheckCommandContextPayload(
    CommandContextField Field,
    bool? ExpectedBoolean,
    Direction10? ExpectedDirection) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.CheckCommandContext;
}
