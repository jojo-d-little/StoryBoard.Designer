using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed record CancelTimerPayload(string TimerKey, TimerOwnerType? ScopeQualifierKind, Guid? ScopeQualifierId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.CancelTimer;
}
