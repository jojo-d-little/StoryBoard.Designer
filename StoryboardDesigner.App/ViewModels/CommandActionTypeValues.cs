using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public static class CommandActionTypeValues
{
    public static IReadOnlyList<CommandActionType> All { get; } =
    [
        CommandActionType.LinkedActions,
        CommandActionType.Synonym,
        CommandActionType.EchoMessage,
        CommandActionType.NavigateDirection,
        CommandActionType.NavigateToAdjacent,
        CommandActionType.MoveRoomObjectOnGrid,
        CommandActionType.MoveRoomObjectByPoints,
        CommandActionType.RotateRoomObjectOnGrid,
        CommandActionType.StackRoomObjectOnAnother,
        CommandActionType.SetGameProperty,
        CommandActionType.CheckGameProperty,
        CommandActionType.PlaySoundEffect,
        CommandActionType.PutObjectInContainer,
        CommandActionType.RemoveObjectFromContainer,
        CommandActionType.OpenObject,
        CommandActionType.CloseObject,
        CommandActionType.UnlockObject,
        CommandActionType.LockObject,
        CommandActionType.SetActiveRoomObject,
        CommandActionType.SelectRoomObjectByPoint,
        CommandActionType.ClearRoomObjectSelections,
        CommandActionType.NextPhase,
        CommandActionType.PreviousPhase,
        CommandActionType.SetPhase,
        CommandActionType.ManageNarrativePhaseAmbientSounds,
        CommandActionType.StartTimer,
        CommandActionType.CancelTimer,
        CommandActionType.InvokeProcedure,
        CommandActionType.MaterializeObjectCopy,
        CommandActionType.BuildCompositeByTarget,
        CommandActionType.BuildCompositeByParts,
        CommandActionType.BreakCompositeItem
    ];

    public static IReadOnlyList<CommandActionType> LinkedFlowAllowed { get; } =
    [
        CommandActionType.LinkedActions,
        CommandActionType.EchoMessage,
        CommandActionType.NavigateDirection,
        CommandActionType.NavigateToAdjacent,
        CommandActionType.MoveRoomObjectOnGrid,
        CommandActionType.MoveRoomObjectByPoints,
        CommandActionType.RotateRoomObjectOnGrid,
        CommandActionType.StackRoomObjectOnAnother,
        CommandActionType.SetGameProperty,
        CommandActionType.CheckGameProperty,
        CommandActionType.PlaySoundEffect,
        CommandActionType.PutObjectInContainer,
        CommandActionType.RemoveObjectFromContainer,
        CommandActionType.OpenObject,
        CommandActionType.CloseObject,
        CommandActionType.UnlockObject,
        CommandActionType.LockObject,
        CommandActionType.SetActiveRoomObject,
        CommandActionType.SelectRoomObjectByPoint,
        CommandActionType.ClearRoomObjectSelections,
        CommandActionType.NextPhase,
        CommandActionType.PreviousPhase,
        CommandActionType.SetPhase,
        CommandActionType.ManageNarrativePhaseAmbientSounds,
        CommandActionType.StartTimer,
        CommandActionType.CancelTimer,
        CommandActionType.InvokeProcedure,
        CommandActionType.MaterializeObjectCopy,
        CommandActionType.BuildCompositeByTarget,
        CommandActionType.BuildCompositeByParts,
        CommandActionType.BreakCompositeItem
    ];
}
