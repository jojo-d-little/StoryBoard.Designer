using System.Collections.Generic;

namespace StoryboardDesigner.App.Models;

public static class ActionPayloadSchemaMap
{
    private static readonly IReadOnlyDictionary<CommandActionType, ActionPayloadSchemaEntry> Entries =
        new Dictionary<CommandActionType, ActionPayloadSchemaEntry>
        {
            [CommandActionType.LinkedActions] = new(CommandActionType.LinkedActions, typeof(LinkedFlowPayload), [nameof(CommandAction.LinkedActions)]),
            [CommandActionType.Synonym] = new(CommandActionType.Synonym, typeof(SynonymPayload), [nameof(CommandAction.SynonymTargetActionId)]),
            [CommandActionType.EchoMessage] = new(CommandActionType.EchoMessage, typeof(EchoPayload), []),
            [CommandActionType.SetFlag] = new(CommandActionType.SetFlag, typeof(SetFlagPayload), [nameof(CommandAction.FlagName), nameof(CommandAction.FlagValue)]),
            [CommandActionType.CheckGameProperty] = new(CommandActionType.CheckGameProperty, typeof(CheckGamePropertyPayload), [nameof(CommandAction.FlagName), nameof(CommandAction.FlagValue)]),
            [CommandActionType.SetGameProperty] = new(CommandActionType.SetGameProperty, typeof(SetGamePropertyPayload), [nameof(CommandAction.GamePropertyName), nameof(CommandAction.GamePropertyValue)]),
            [CommandActionType.PutObjectInContainer] = new(CommandActionType.PutObjectInContainer, typeof(ContainerTransferPayload), [nameof(CommandAction.TargetContainerId)]),
            [CommandActionType.RemoveObjectFromContainer] = new(CommandActionType.RemoveObjectFromContainer, typeof(ContainerTransferPayload), [nameof(CommandAction.TargetContainerId)]),
            [CommandActionType.MoveRoomObjectOnGrid] = new(CommandActionType.MoveRoomObjectOnGrid, typeof(MoveRoomObjectOnGridPayload), [nameof(CommandAction.MoveDirectionToken), nameof(CommandAction.MoveDistanceInCells), nameof(CommandAction.MoveAllowPartialMove), nameof(CommandAction.MoveAllowJumpOver), nameof(CommandAction.MoveVisualTransitionHint), nameof(CommandAction.MoveTravelVisualizationMode)]),
            [CommandActionType.MoveRoomObjectByPoints] = new(CommandActionType.MoveRoomObjectByPoints, typeof(MoveRoomObjectByPointsPayload), [nameof(CommandAction.MoveAllowPartialMove), nameof(CommandAction.MoveAllowJumpOver), nameof(CommandAction.MoveVisualTransitionHint), nameof(CommandAction.MoveTravelVisualizationMode)]),
            [CommandActionType.RotateRoomObjectOnGrid] = new(CommandActionType.RotateRoomObjectOnGrid, typeof(RotateRoomObjectOnGridPayload), [nameof(CommandAction.RotateMode), nameof(CommandAction.RotateTurnDegrees), nameof(CommandAction.RotateFacingDirectionToken), nameof(CommandAction.RotateVisualTransitionHint)]),
            [CommandActionType.StackRoomObjectOnAnother] = new(CommandActionType.StackRoomObjectOnAnother, typeof(StackRoomObjectOnAnotherPayload), [nameof(CommandAction.StackVisualTransitionHint)]),
            [CommandActionType.OpenObject] = new(CommandActionType.OpenObject, typeof(EchoPayload), []),
            [CommandActionType.CloseObject] = new(CommandActionType.CloseObject, typeof(EchoPayload), []),
            [CommandActionType.UnlockObject] = new(CommandActionType.UnlockObject, typeof(EchoPayload), []),
            [CommandActionType.LockObject] = new(CommandActionType.LockObject, typeof(EchoPayload), []),
            [CommandActionType.SetActiveRoomObject] = new(CommandActionType.SetActiveRoomObject, typeof(SetActiveRoomObjectPayload), [nameof(CommandAction.SelectionCueEffectKey)]),
            [CommandActionType.SelectRoomObjectByPoint] = new(CommandActionType.SelectRoomObjectByPoint, typeof(SelectRoomObjectByPointPayload), [nameof(CommandAction.SelectionCueEffectKey)]),
            [CommandActionType.ClearActiveRoomObjects] = new(CommandActionType.ClearActiveRoomObjects, typeof(ClearActiveRoomObjectsPayload), [nameof(CommandAction.ClearActiveRoomObjectsScope)]),
            [CommandActionType.ClearRoomObjectSelections] = new(CommandActionType.ClearRoomObjectSelections, typeof(ClearRoomObjectSelectionsPayload), []),
            [CommandActionType.StartTimer] = new(CommandActionType.StartTimer, typeof(StartTimerPayload), []),
            [CommandActionType.CancelTimer] = new(CommandActionType.CancelTimer, typeof(CancelTimerPayload), []),
            [CommandActionType.ManageNarrativePhaseAmbientSounds] = new(CommandActionType.ManageNarrativePhaseAmbientSounds, typeof(EchoPayload), []),
            [CommandActionType.NavigateDirection] = new(CommandActionType.NavigateDirection, typeof(NavigatePayload), []),
            [CommandActionType.NavigateToAdjacent] = new(CommandActionType.NavigateToAdjacent, typeof(NavigatePayload), []),
            [CommandActionType.MaterializeObjectCopy] = new(CommandActionType.MaterializeObjectCopy, typeof(MaterializeObjectCopyPayload), [nameof(CommandAction.MaterializeSourceObjectId)]),
            [CommandActionType.InvokeProcedure] = new(CommandActionType.InvokeProcedure, typeof(InvokeProcedurePayload), [nameof(CommandAction.ProcedureId)]),
            [CommandActionType.BuildCompositeByTarget] = new(CommandActionType.BuildCompositeByTarget, typeof(CompositeByTargetPayload), [nameof(CommandAction.CompositeTargetObjectId), nameof(CommandAction.CompositeRecipeId), nameof(CommandAction.CompositeRequiredPartObjectIds), nameof(CommandAction.CompositeStrictPartCountEnforcement), nameof(CommandAction.CompositeMinimumRequiredPartCount), nameof(CommandAction.CompositePartConsumptionMode)]),
            [CommandActionType.BuildCompositeByParts] = new(CommandActionType.BuildCompositeByParts, typeof(CompositeByPartsPayload), [nameof(CommandAction.CompositeTargetObjectId), nameof(CommandAction.CompositeRecipeId), nameof(CommandAction.CompositeRequiredPartObjectIds), nameof(CommandAction.CompositeStrictPartCountEnforcement), nameof(CommandAction.CompositeMinimumRequiredPartCount), nameof(CommandAction.CompositeMatchMode), nameof(CommandAction.CompositeAmbiguityPolicy), nameof(CommandAction.CompositePartConsumptionMode), nameof(CommandAction.CompositeResolvedTargetOutputTemplate)]),
            [CommandActionType.BreakCompositeItem] = new(CommandActionType.BreakCompositeItem, typeof(BreakCompositePayload), [nameof(CommandAction.CompositeTargetObjectId), nameof(CommandAction.CompositeRecipeId), nameof(CommandAction.CompositeRequiredPartObjectIds), nameof(CommandAction.CompositeStrictPartCountEnforcement), nameof(CommandAction.CompositeMinimumRequiredPartCount), nameof(CommandAction.CompositePartConsumptionMode), nameof(CommandAction.OutcomeMessageMap)])
        };

    public static ActionPayloadSchemaEntry Get(CommandActionType actionType)
    {
        return Entries.TryGetValue(actionType, out var entry)
            ? entry
            : new ActionPayloadSchemaEntry(actionType, null, []);
    }

    public static bool IsPayloadCompatible(CommandActionType actionType, IActionPayload payload)
    {
        var entry = Get(actionType);
        return entry.PayloadType is not null && entry.PayloadType.IsInstanceOfType(payload);
    }
}
