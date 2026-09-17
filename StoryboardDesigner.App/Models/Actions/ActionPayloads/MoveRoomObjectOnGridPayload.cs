using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameServices.References;
using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Models;

public sealed record MoveRoomObjectOnGridPayload : IActionPayload
{
    public MoveRoomObjectOnGridPayload(
        string directionToken = "",
        int distanceInCells = 1,
        bool allowPartialMove = false,
        bool allowJumpOver = false,
        RuntimeMovementVisualTransitionHint visualTransitionHint = RuntimeMovementVisualTransitionHint.Medium,
        RuntimeMovementTravelVisualizationMode travelVisualizationMode = RuntimeMovementTravelVisualizationMode.LegByLeg)
    {
        DirectionToken = directionToken?.Trim() ?? string.Empty;
        DistanceInCells = distanceInCells < 1 ? 1 : distanceInCells;
        AllowPartialMove = allowPartialMove;
        AllowJumpOver = allowJumpOver;
        VisualTransitionHint = visualTransitionHint;
        TravelVisualizationMode = travelVisualizationMode;
    }

    public CommandActionType ActionType => CommandActionType.MoveRoomObjectOnGrid;

    public string DirectionToken { get; init; }

    public int DistanceInCells { get; init; }

    public bool AllowPartialMove { get; init; }

    public bool AllowJumpOver { get; init; }

    public RuntimeMovementVisualTransitionHint VisualTransitionHint { get; init; }

    public RuntimeMovementTravelVisualizationMode TravelVisualizationMode { get; init; }

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}