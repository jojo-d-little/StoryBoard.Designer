using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Models;

public sealed record MoveRoomObjectOnGridPayload : IActionPayload
{
    public MoveRoomObjectOnGridPayload(
        string directionToken = "",
        int distanceInCells = 1,
        bool allowPartialMove = false,
        bool allowJumpOver = false,
        string movementEffectKey = MovementPresentationEffectKeys.DefaultMedium,
        MovementTravelVisualizationMode travelVisualizationMode = MovementTravelVisualizationMode.LegByLeg,
        Guid? targetObjectId = null)
    {
        DirectionToken = directionToken?.Trim() ?? string.Empty;
        DistanceInCells = distanceInCells < 1 ? 1 : distanceInCells;
        AllowPartialMove = allowPartialMove;
        AllowJumpOver = allowJumpOver;
        MovementEffectKey = movementEffectKey?.Trim() ?? string.Empty;
        TravelVisualizationMode = travelVisualizationMode;
        TargetObjectId = targetObjectId is Guid id && id != Guid.Empty ? id : null;
    }

    public CommandActionType ActionType => CommandActionType.MoveRoomObjectOnGrid;

    public string DirectionToken { get; init; }

    public int DistanceInCells { get; init; }

    public bool AllowPartialMove { get; init; }

    public bool AllowJumpOver { get; init; }

    public string MovementEffectKey { get; init; }

    public MovementTravelVisualizationMode TravelVisualizationMode { get; init; }

    public Guid? TargetObjectId { get; init; }

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
