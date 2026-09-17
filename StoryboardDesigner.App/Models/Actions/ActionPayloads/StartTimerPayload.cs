using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed record StartTimerPayload(string TimerKey, TimerOwnerType? OwnerScopeKindOverride) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.StartTimer;
}
