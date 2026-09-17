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
            return payload.PropertyName ?? string.Empty;
        }

        return action.FlagName ?? string.Empty;
    }

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

    public static void SetCheckGameProperty(CommandAction action, string propertyName, bool expectedValue)
    {
        var normalizedName = propertyName ?? string.Empty;
        action.FlagName = normalizedName;
        action.FlagValue = expectedValue;

        if (action.ActionType == CommandActionType.CheckGameProperty)
        {
            action.Payload = new CheckGamePropertyPayload(normalizedName, expectedValue);
        }
    }

    public static string GetSetPropertyName(CommandAction action)
    {
        if (action.Payload is SetGamePropertyPayload payload)
        {
            return payload.PropertyName ?? string.Empty;
        }

        return action.GamePropertyName ?? string.Empty;
    }

    public static string GetSetPropertyValue(CommandAction action)
    {
        if (action.Payload is SetGamePropertyPayload payload)
        {
            return payload.PropertyValue ?? string.Empty;
        }

        return action.GamePropertyValue ?? string.Empty;
    }

    public static void SetSetGameProperty(CommandAction action, string propertyName, string propertyValue)
    {
        var normalizedName = propertyName ?? string.Empty;
        var normalizedValue = propertyValue ?? string.Empty;
        action.GamePropertyName = normalizedName;
        action.GamePropertyValue = normalizedValue;

        if (action.ActionType == CommandActionType.SetGameProperty)
        {
            action.Payload = new SetGamePropertyPayload(normalizedName, normalizedValue);
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

        return new MoveRoomObjectOnGridPayload(action.DirectionQualifierText);
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
                VisualTransitionHint: movePayload.VisualTransitionHint,
                TravelVisualizationMode: movePayload.TravelVisualizationMode);
        }

        return new MoveRoomObjectByPointsPayload(
            TargetResolutionIntent: "PrimarySelection",
            AllowPartialMove: action.MoveAllowPartialMove,
            AllowJumpOver: action.MoveAllowJumpOver,
            VisualTransitionHint: action.MoveVisualTransitionHint,
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
            ? RuntimeRotateRoomObjectOnGridAttemptMode.Turn
            : RuntimeRotateRoomObjectOnGridAttemptMode.Face;

        return new RotateRoomObjectOnGridPayload(
            mode: fallbackMode,
            turnDegrees: 90,
            facingDirectionToken: fallbackFacing,
            visualTransitionHint: Storyboard.Shared.GameServices.RuntimeContext.RuntimeMovementVisualTransitionHint.Medium);
    }

    public static StackRoomObjectOnAnotherPayload GetStackRoomObjectOnAnotherPayload(CommandAction action)
    {
        if (action.Payload is StackRoomObjectOnAnotherPayload payload)
        {
            return payload;
        }

        return new StackRoomObjectOnAnotherPayload(
            RuntimeMovementVisualTransitionHint.Medium);
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
        Storyboard.Shared.GameServices.RuntimeContext.RuntimeMovementVisualTransitionHint visualTransitionHint,
        bool allowJumpOver = false,
        Storyboard.Shared.GameServices.RuntimeContext.RuntimeMovementTravelVisualizationMode travelVisualizationMode = Storyboard.Shared.GameServices.RuntimeContext.RuntimeMovementTravelVisualizationMode.LegByLeg)
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
            visualTransitionHint,
            travelVisualizationMode);
    }

    public static void SetMoveRoomObjectByPoints(
        CommandAction action,
        string targetResolutionIntent,
        bool allowPartialMove,
        bool allowJumpOver,
        RuntimeMovementVisualTransitionHint visualTransitionHint,
        RuntimeMovementTravelVisualizationMode travelVisualizationMode = RuntimeMovementTravelVisualizationMode.LegByLeg)
    {
        if (action.ActionType != CommandActionType.MoveRoomObjectByPoints)
        {
            return;
        }

        action.Payload = new MoveRoomObjectByPointsPayload(
            targetResolutionIntent?.Trim() ?? string.Empty,
            allowPartialMove,
            allowJumpOver,
            visualTransitionHint,
            travelVisualizationMode);
    }

    public static void SetRotateRoomObjectOnGrid(
        CommandAction action,
        RuntimeRotateRoomObjectOnGridAttemptMode mode,
        int? turnDegrees,
        string facingDirectionToken,
        Storyboard.Shared.GameServices.RuntimeContext.RuntimeMovementVisualTransitionHint visualTransitionHint)
    {
        if (action.ActionType != CommandActionType.RotateRoomObjectOnGrid)
        {
            return;
        }

        action.Payload = new RotateRoomObjectOnGridPayload(
            mode,
            turnDegrees,
            NormalizeRotateFacingDirectionToken(facingDirectionToken),
            visualTransitionHint);
    }

    public static void SetStackRoomObjectOnAnother(
        CommandAction action,
        RuntimeMovementVisualTransitionHint visualTransitionHint)
    {
        if (action.ActionType != CommandActionType.StackRoomObjectOnAnother)
        {
            return;
        }

        action.Payload = new StackRoomObjectOnAnotherPayload(visualTransitionHint);
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
        RuntimeClearActiveRoomObjectsScope clearScope)
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

        if (GameCommandDirectionFormatting.TryParseToken(normalized, out var parsed)
            && IsEightWayDirection(parsed))
        {
            return parsed.ToToken();
        }

        return Enum.TryParse<GameCommandDirection>(normalized, true, out var byEnumName)
            && IsEightWayDirection(byEnumName)
            ? byEnumName.ToToken()
            : normalized;
    }

    private static bool IsEightWayDirection(GameCommandDirection direction)
    {
        return direction is GameCommandDirection.North
            or GameCommandDirection.NorthEast
            or GameCommandDirection.East
            or GameCommandDirection.SouthEast
            or GameCommandDirection.South
            or GameCommandDirection.SouthWest
            or GameCommandDirection.West
            or GameCommandDirection.NorthWest;
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
}
