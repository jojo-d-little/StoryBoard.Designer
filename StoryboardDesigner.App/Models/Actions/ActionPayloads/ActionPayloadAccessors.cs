namespace StoryboardDesigner.App.Models;

public static class ActionPayloadAccessors
{
    public static Guid? GetSynonymTargetActionId(CommandAction action)
    {
        return action.GetSynonymTargetActionId();
    }

    public static IReadOnlyList<LinkedActionReference> GetLinkedActions(CommandAction action)
    {
        return action.GetLinkedActions();
    }

    public static string GetEchoMessage(CommandAction action)
    {
        return action.GetOutcomeScript("Success");
    }

    public static void SetEchoMessage(CommandAction action, string value)
    {
        var normalized = value ?? string.Empty;
        action.SetOutcomeScript("Success", normalized);

        if (action.ActionType == CommandActionType.EchoMessage)
        {
            action.Payload = new EchoPayload();
        }
    }

    public static string GetCheckPropertyName(CommandAction action)
    {
        if (action.Payload is CheckGamePropertyPayload payload)
        {
            return !string.IsNullOrWhiteSpace(payload.PropertyName)
                ? payload.PropertyName
                : payload.GamePropertyReference?.PropertyName ?? string.Empty;
        }

        return action.FlagName ?? string.Empty;
    }

    public static Storyboard.Shared.DesignerContracts.ProjectGamePropertySourceReferenceDto? GetCheckGamePropertyReference(CommandAction action) =>
        action.Payload is CheckGamePropertyPayload payload ? payload.GamePropertyReference : null;

    public static bool GetCheckExpectedValue(CommandAction action)
    {
        if (action.Payload is CheckGamePropertyPayload payload)
        {
            return payload.ExpectedValue;
        }

        return action.FlagValue;
    }

    public static string GetSetFlagName(CommandAction action)
    {
        if (action.Payload is SetFlagPayload payload)
        {
            return payload.FlagName ?? string.Empty;
        }

        return action.FlagName ?? string.Empty;
    }

    public static bool GetSetFlagValue(CommandAction action)
    {
        if (action.Payload is SetFlagPayload payload)
        {
            return payload.FlagValue;
        }

        return action.FlagValue;
    }

    public static void SetSetFlag(CommandAction action, string flagName, bool flagValue)
    {
        var normalizedName = flagName ?? string.Empty;
        action.FlagName = normalizedName;
        action.FlagValue = flagValue;

        if (action.ActionType == CommandActionType.SetFlag)
        {
            action.Payload = new SetFlagPayload(normalizedName, flagValue);
        }
    }

    public static void SetCheckGameProperty(
        CommandAction action,
        string propertyName,
        bool expectedValue,
        Storyboard.Shared.DesignerContracts.ProjectGamePropertySourceReferenceDto? gamePropertyReference = null)
    {
        var normalizedName = propertyName ?? string.Empty;
        action.FlagName = normalizedName;
        action.FlagValue = expectedValue;

        if (action.ActionType == CommandActionType.CheckGameProperty)
        {
            gamePropertyReference ??= GetCheckGamePropertyReference(action);
            action.Payload = new CheckGamePropertyPayload(normalizedName, expectedValue, gamePropertyReference);
        }
    }

    public static CheckCommandContextPayload GetCheckCommandContext(CommandAction action)
    {
        return action.Payload is CheckCommandContextPayload payload
            ? payload
            : new CheckCommandContextPayload(
                Storyboard.Shared.RuntimeContracts.Enums.CommandContextField.DirectionSpecified,
                null,
                null);
    }

    public static void SetCheckCommandContext(
        CommandAction action,
        Storyboard.Shared.RuntimeContracts.Enums.CommandContextField field,
        bool? expectedBoolean,
        Storyboard.Shared.RuntimeContracts.Enums.Direction10? expectedDirection)
    {
        if (action.ActionType == CommandActionType.CheckCommandContext)
        {
            action.Payload = new CheckCommandContextPayload(field, expectedBoolean, expectedDirection);
        }
    }

    public static string GetSetPropertyName(CommandAction action)
    {
        if (action.Payload is SetGamePropertyPayload payload)
        {
            return !string.IsNullOrWhiteSpace(payload.PropertyName)
                ? payload.PropertyName
                : payload.GamePropertyReference?.PropertyName ?? string.Empty;
        }

        return action.GamePropertyName ?? string.Empty;
    }

    public static Storyboard.Shared.DesignerContracts.ProjectGamePropertySourceReferenceDto? GetSetGamePropertyReference(CommandAction action) =>
        action.Payload is SetGamePropertyPayload payload ? payload.GamePropertyReference : null;

    public static string GetSetPropertyValue(CommandAction action)
    {
        if (action.Payload is SetGamePropertyPayload payload)
        {
            return payload.PropertyValue ?? string.Empty;
        }

        return action.GamePropertyValue ?? string.Empty;
    }

    public static void SetSetGameProperty(
        CommandAction action,
        string propertyName,
        string propertyValue,
        Storyboard.Shared.DesignerContracts.ProjectGamePropertySourceReferenceDto? gamePropertyReference = null)
    {
        var normalizedName = propertyName ?? string.Empty;
        var normalizedValue = propertyValue ?? string.Empty;
        action.GamePropertyName = normalizedName;
        action.GamePropertyValue = normalizedValue;

        if (action.ActionType == CommandActionType.SetGameProperty)
        {
            gamePropertyReference ??= GetSetGamePropertyReference(action);
            action.Payload = new SetGamePropertyPayload(normalizedName, normalizedValue, gamePropertyReference);
        }
    }

    public static ContainerTransferPayload GetContainerTransfer(CommandAction action)
    {
        if (action.Payload is ContainerTransferPayload payload)
        {
            return new ContainerTransferPayload(payload.TargetContainerId);
        }

        return new ContainerTransferPayload(
            action.TargetContainerId ?? string.Empty);
    }

    public static NavigatePayload GetNavigatePayload(CommandAction action)
    {
        if (action.Payload is NavigatePayload payload)
        {
            return payload;
        }

        return new NavigatePayload();
    }

    public static MoveRoomObjectOnGridPayload GetMoveRoomObjectOnGridPayload(CommandAction action)
    {
        if (action.Payload is MoveRoomObjectOnGridPayload payload)
        {
            return payload;
        }

        return new MoveRoomObjectOnGridPayload(action.DirectionQualifierText, targetObjectId: action.MoveTargetObjectId);
    }

    public static MoveRoomObjectByPointsPayload GetMoveRoomObjectByPointsPayload(CommandAction action)
    {
        if (action.Payload is MoveRoomObjectByPointsPayload payload)
        {
            return payload;
        }

        if (action.Payload is MoveRoomObjectOnGridPayload movePayload)
        {
            return new MoveRoomObjectByPointsPayload(
                TargetResolutionIntent: "PrimarySelection",
                AllowPartialMove: movePayload.AllowPartialMove,
                AllowJumpOver: movePayload.AllowJumpOver,
                MovementEffectKey: movePayload.MovementEffectKey,
                TravelVisualizationMode: movePayload.TravelVisualizationMode);
        }

        return new MoveRoomObjectByPointsPayload(
            TargetResolutionIntent: "PrimarySelection",
            AllowPartialMove: action.MoveAllowPartialMove,
            AllowJumpOver: action.MoveAllowJumpOver,
            MovementEffectKey: action.MoveMovementEffectKey,
            TravelVisualizationMode: action.MoveTravelVisualizationMode);
    }

    public static RotateRoomObjectOnGridPayload GetRotateRoomObjectOnGridPayload(CommandAction action)
    {
        if (action.Payload is RotateRoomObjectOnGridPayload payload)
        {
            return payload;
        }

        var fallbackFacing = NormalizeRotateFacingDirectionToken(action.DirectionQualifierText);
        var fallbackMode = string.IsNullOrWhiteSpace(fallbackFacing)
            ? RoomObjectRotationMode.Turn
            : RoomObjectRotationMode.Face;

        return new RotateRoomObjectOnGridPayload(
            mode: fallbackMode,
            turnDegrees: 90,
            facingDirectionToken: fallbackFacing,
            movementEffectKey: MovementPresentationEffectKeys.DefaultMedium,
            targetObjectId: action.RotateTargetObjectId);
    }

    public static StackRoomObjectOnAnotherPayload GetStackRoomObjectOnAnotherPayload(CommandAction action)
    {
        if (action.Payload is StackRoomObjectOnAnotherPayload payload)
        {
            return payload;
        }

        return new StackRoomObjectOnAnotherPayload(
            MovementPresentationEffectKeys.DefaultMedium);
    }

    public static SetActiveRoomObjectPayload GetSetActiveRoomObjectPayload(CommandAction action)
    {
        if (action.Payload is SetActiveRoomObjectPayload payload)
        {
            return payload;
        }

        return new SetActiveRoomObjectPayload(action.SelectionCueEffectKey ?? string.Empty);
    }

    public static SelectRoomObjectByPointPayload GetSelectRoomObjectByPointPayload(CommandAction action)
    {
        if (action.Payload is SelectRoomObjectByPointPayload payload)
        {
            return payload;
        }

        if (action.Payload is SetActiveRoomObjectPayload setActivePayload)
        {
            return new SelectRoomObjectByPointPayload(setActivePayload.SelectionCueEffectKey ?? string.Empty);
        }

        return new SelectRoomObjectByPointPayload(action.SelectionCueEffectKey ?? string.Empty);
    }

    public static ClearActiveRoomObjectsPayload GetClearActiveRoomObjectsPayload(CommandAction action)
    {
        if (action.Payload is ClearActiveRoomObjectsPayload payload)
        {
            return payload;
        }

        return new ClearActiveRoomObjectsPayload(action.ClearActiveRoomObjectsScope);
    }

    public static ClearRoomObjectSelectionsPayload GetClearRoomObjectSelectionsPayload(CommandAction action)
    {
        if (action.Payload is ClearRoomObjectSelectionsPayload payload)
        {
            return payload;
        }

        return new ClearRoomObjectSelectionsPayload(ClearRoomObjectSelectionsScope.All);
    }

    public static void SetMoveRoomObjectOnGrid(
        CommandAction action,
        string directionToken,
        int distanceInCells,
        bool allowPartialMove,
        string movementEffectKey,
        bool allowJumpOver = false,
        MovementTravelVisualizationMode travelVisualizationMode = MovementTravelVisualizationMode.LegByLeg,
        Guid? targetObjectId = null)
    {
        if (action.ActionType != CommandActionType.MoveRoomObjectOnGrid)
        {
            return;
        }

        action.Payload = new MoveRoomObjectOnGridPayload(
            directionToken,
            distanceInCells,
            allowPartialMove,
            allowJumpOver,
            movementEffectKey,
            travelVisualizationMode,
            targetObjectId);
    }

    public static void SetMoveRoomObjectByPoints(
        CommandAction action,
        string targetResolutionIntent,
        bool allowPartialMove,
        bool allowJumpOver,
        string movementEffectKey,
        MovementTravelVisualizationMode travelVisualizationMode = MovementTravelVisualizationMode.LegByLeg)
    {
        if (action.ActionType != CommandActionType.MoveRoomObjectByPoints)
        {
            return;
        }

        action.Payload = new MoveRoomObjectByPointsPayload(
            targetResolutionIntent?.Trim() ?? string.Empty,
            allowPartialMove,
            allowJumpOver,
            movementEffectKey,
            travelVisualizationMode);
    }

    public static void SetRotateRoomObjectOnGrid(
        CommandAction action,
        RoomObjectRotationMode mode,
        int? turnDegrees,
        string facingDirectionToken,
        string movementEffectKey,
        Guid? targetObjectId = null)
    {
        if (action.ActionType != CommandActionType.RotateRoomObjectOnGrid)
        {
            return;
        }

        action.Payload = new RotateRoomObjectOnGridPayload(
            mode,
            turnDegrees,
            NormalizeRotateFacingDirectionToken(facingDirectionToken),
            movementEffectKey,
            targetObjectId);
    }

    public static void SetStackRoomObjectOnAnother(
        CommandAction action,
        string movementEffectKey)
    {
        if (action.ActionType != CommandActionType.StackRoomObjectOnAnother)
        {
            return;
        }

        action.Payload = new StackRoomObjectOnAnotherPayload(movementEffectKey);
    }

    public static void SetSetActiveRoomObject(
        CommandAction action,
        string selectionCueEffectKey)
    {
        if (action.ActionType != CommandActionType.SetActiveRoomObject)
        {
            return;
        }

        var normalized = selectionCueEffectKey?.Trim() ?? string.Empty;
        action.Payload = new SetActiveRoomObjectPayload(normalized);
    }

    public static void SetSelectRoomObjectByPoint(
        CommandAction action,
        string selectionCueEffectKey)
    {
        if (action.ActionType != CommandActionType.SelectRoomObjectByPoint)
        {
            return;
        }

        var normalized = selectionCueEffectKey?.Trim() ?? string.Empty;
        action.Payload = new SelectRoomObjectByPointPayload(normalized);
    }

    public static void SetClearActiveRoomObjects(
        CommandAction action,
        ClearRoomObjectSelectionsScope clearScope)
    {
        if (action.ActionType != CommandActionType.ClearActiveRoomObjects)
        {
            return;
        }

        action.Payload = new ClearActiveRoomObjectsPayload(clearScope);
    }

    public static void SetClearRoomObjectSelections(
        CommandAction action,
        ClearRoomObjectSelectionsScope clearScope)
    {
        if (action.ActionType != CommandActionType.ClearRoomObjectSelections)
        {
            return;
        }

        action.Payload = new ClearRoomObjectSelectionsPayload(clearScope);
    }

    private static string NormalizeRotateFacingDirectionToken(string? token)
    {
        var normalized = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var compact = new string(normalized
            .ToUpperInvariant()
            .Where(static character => !char.IsWhiteSpace(character) && character != '-' && character != '_')
            .ToArray());
        var mapped = compact switch
        {
            "N" or "NORTH" => "N",
            "NE" or "NORTHEAST" => "NE",
            "E" or "EAST" => "E",
            "SE" or "SOUTHEAST" => "SE",
            "S" or "SOUTH" => "S",
            "SW" or "SOUTHWEST" => "SW",
            "W" or "WEST" => "W",
            "NW" or "NORTHWEST" => "NW",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        if (FoundationDirection10Formatting.TryParseToken(normalized, out var parsed)
            && IsEightWayDirection(parsed))
        {
            return FoundationDirection10Formatting.ToToken(parsed);
        }

        return Enum.TryParse<Direction10>(normalized, true, out var byEnumName)
            && IsEightWayDirection(byEnumName)
            ? FoundationDirection10Formatting.ToToken(byEnumName)
            : normalized;
    }

    private static bool IsEightWayDirection(Direction10 direction)
    {
        return direction is Direction10.North
            or Direction10.NorthEast
            or Direction10.East
            or Direction10.SouthEast
            or Direction10.South
            or Direction10.SouthWest
            or Direction10.West
            or Direction10.NorthWest;
    }

    public static CompositeByTargetPayload GetCompositeByTargetPayload(CommandAction action)
    {
        if (action.Payload is CompositeByTargetPayload payload)
        {
            return payload;
        }

        return new CompositeByTargetPayload(
            action.CompositeTargetObjectId,
            action.CompositeRecipeId,
            action.CompositeRequiredPartObjectIds.ToList(),
            action.CompositeStrictPartCountEnforcement,
            action.CompositeMinimumRequiredPartCount,
            action.CompositePartConsumptionMode ?? string.Empty);
    }

    public static CompositeByPartsPayload GetCompositeByPartsPayload(CommandAction action)
    {
        if (action.Payload is CompositeByPartsPayload payload)
        {
            return payload;
        }

        return new CompositeByPartsPayload(
            action.CompositeTargetObjectId,
            action.CompositeRecipeId,
            action.CompositeRequiredPartObjectIds.ToList(),
            action.CompositeStrictPartCountEnforcement,
            action.CompositeMinimumRequiredPartCount,
            action.CompositeMatchMode ?? string.Empty,
            action.CompositeAmbiguityPolicy ?? string.Empty,
            action.CompositePartConsumptionMode ?? string.Empty,
            action.CompositeResolvedTargetOutputTemplate ?? string.Empty);
    }

    public static BreakCompositePayload GetBreakCompositePayload(CommandAction action)
    {
        if (action.Payload is BreakCompositePayload payload)
        {
            return payload;
        }

        return new BreakCompositePayload(
            action.CompositeTargetObjectId,
            action.CompositeRecipeId,
            action.CompositeRequiredPartObjectIds.ToList(),
            action.CompositeStrictPartCountEnforcement,
            action.CompositeMinimumRequiredPartCount,
            action.CompositePartConsumptionMode ?? string.Empty);
    }

    public static Guid? GetMaterializeSourceObjectId(CommandAction action)
    {
        if (action.Payload is MaterializeObjectCopyPayload payload)
        {
            return payload.MaterializeSourceObjectId;
        }

        return action.MaterializeSourceObjectId;
    }

    public static void SetMaterializeSourceObjectId(CommandAction action, Guid? sourceObjectId)
    {
        action.MaterializeSourceObjectId = sourceObjectId;

        if (action.ActionType == CommandActionType.MaterializeObjectCopy)
        {
            action.Payload = new MaterializeObjectCopyPayload(sourceObjectId);
        }
    }

    public static Guid? GetProcedureId(CommandAction action)
    {
        if (action.Payload is InvokeProcedurePayload payload)
        {
            return payload.ProcedureId;
        }

        return action.ProcedureId;
    }

    public static void SetProcedureId(CommandAction action, Guid? procedureId)
    {
        action.ProcedureId = procedureId;

        if (action.ActionType == CommandActionType.InvokeProcedure)
        {
            action.Payload = new InvokeProcedurePayload(procedureId);
        }
    }

    public static string GetStartTimerKey(CommandAction action)
    {
        if (action.Payload is StartTimerPayload payload)
        {
            return payload.TimerKey ?? string.Empty;
        }

        return string.Empty;
    }

    public static TimerOwnerType? GetStartTimerOwnerScopeKindOverride(CommandAction action)
    {
        if (action.Payload is StartTimerPayload payload)
        {
            return payload.OwnerScopeKindOverride;
        }

        return null;
    }

    public static void SetStartTimer(CommandAction action, string timerKey, TimerOwnerType? ownerScopeKindOverride)
    {
        if (action.ActionType != CommandActionType.StartTimer)
        {
            return;
        }

        action.Payload = new StartTimerPayload(timerKey?.Trim() ?? string.Empty, ownerScopeKindOverride);
    }

    public static string GetCancelTimerKey(CommandAction action)
    {
        if (action.Payload is CancelTimerPayload payload)
        {
            return payload.TimerKey ?? string.Empty;
        }

        return string.Empty;
    }

    public static TimerOwnerType? GetCancelTimerScopeQualifierKind(CommandAction action)
    {
        if (action.Payload is CancelTimerPayload payload)
        {
            return payload.ScopeQualifierKind;
        }

        return null;
    }

    public static Guid? GetCancelTimerScopeQualifierId(CommandAction action)
    {
        if (action.Payload is CancelTimerPayload payload)
        {
            return payload.ScopeQualifierId;
        }

        return null;
    }

    public static void SetCancelTimer(CommandAction action, string timerKey, TimerOwnerType? scopeQualifierKind, Guid? scopeQualifierId)
    {
        if (action.ActionType != CommandActionType.CancelTimer)
        {
            return;
        }

        action.Payload = new CancelTimerPayload(timerKey?.Trim() ?? string.Empty, scopeQualifierKind, scopeQualifierId);
    }

    public static Guid GetStartSequenceId(CommandAction action)
    {
        return action.Payload is StartSequencePayload payload ? payload.SequenceId : Guid.Empty;
    }

    public static void SetStartSequenceId(CommandAction action, Guid sequenceId)
    {
        if (action.ActionType == CommandActionType.StartSequence)
        {
            action.Payload = new StartSequencePayload(sequenceId);
        }
    }

    public static Guid GetCancelSequenceId(CommandAction action)
    {
        return action.Payload is CancelSequencePayload payload ? payload.SequenceId : Guid.Empty;
    }

    public static void SetCancelSequenceId(CommandAction action, Guid sequenceId)
    {
        if (action.ActionType == CommandActionType.CancelSequence)
        {
            action.Payload = new CancelSequencePayload(sequenceId);
        }
    }
}
