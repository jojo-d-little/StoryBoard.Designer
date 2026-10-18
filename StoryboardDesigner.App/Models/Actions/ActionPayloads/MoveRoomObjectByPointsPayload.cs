using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Models;

public sealed record MoveRoomObjectByPointsPayload(
    string TargetResolutionIntent,
    bool AllowPartialMove,
    bool AllowJumpOver,
    string MovementEffectKey,
    MovementTravelVisualizationMode TravelVisualizationMode) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.MoveRoomObjectByPoints;
}
