namespace StoryboardDesigner.App.Models;

internal static class CommandActionPayloadCoordinator
{
    private static readonly string[] EchoProjectionProperties =
    [];

    private static readonly string[] LinkedProjectionProperties =
    [
        nameof(CommandAction.LinkedActions)
    ];

    private static readonly string[] SynonymProjectionProperties =
    [
        nameof(CommandAction.SynonymTargetActionId)
    ];

    private static readonly string[] CheckProjectionProperties =
    [
        nameof(CommandAction.FlagName),
        nameof(CommandAction.FlagValue)
    ];

    private static readonly string[] SetFlagProjectionProperties =
    [
        nameof(CommandAction.FlagName),
        nameof(CommandAction.FlagValue)
    ];

    private static readonly string[] SetProjectionProperties =
    [
        nameof(CommandAction.GamePropertyName),
        nameof(CommandAction.GamePropertyValue)
    ];

    private static readonly string[] ContainerProjectionProperties =
    [
        nameof(CommandAction.TargetContainerId)
    ];

    private static readonly string[] MoveProjectionProperties =
    [
        nameof(CommandAction.MoveDirectionToken),
        nameof(CommandAction.MoveDistanceInCells),
        nameof(CommandAction.MoveAllowPartialMove),
        nameof(CommandAction.MoveAllowJumpOver),
        nameof(CommandAction.MoveVisualTransitionHint),
        nameof(CommandAction.MoveTravelVisualizationMode)
    ];

    private static readonly string[] MoveByPointsProjectionProperties =
    [
        nameof(CommandAction.MoveAllowPartialMove),
        nameof(CommandAction.MoveAllowJumpOver),
        nameof(CommandAction.MoveVisualTransitionHint),
        nameof(CommandAction.MoveTravelVisualizationMode)
    ];

    private static readonly string[] RotateProjectionProperties =
    [
        nameof(CommandAction.RotateMode),
        nameof(CommandAction.RotateTurnDegrees),
        nameof(CommandAction.RotateFacingDirectionToken),
        nameof(CommandAction.RotateVisualTransitionHint)
    ];

    private static readonly string[] StackProjectionProperties =
    [
        nameof(CommandAction.StackVisualTransitionHint)
    ];

    private static readonly string[] SetActiveRoomObjectProjectionProperties =
    [
        nameof(CommandAction.SelectionCueEffectKey)
    ];

    private static readonly string[] SelectRoomObjectByPointProjectionProperties =
    [
        nameof(CommandAction.SelectionCueEffectKey)
    ];

    private static readonly string[] ClearActiveRoomObjectsProjectionProperties =
    [
        nameof(CommandAction.ClearActiveRoomObjectsScope)
    ];

    private static readonly string[] StartTimerProjectionProperties =
    [];

    private static readonly string[] CancelTimerProjectionProperties =
    [];

    private static readonly string[] NavigateProjectionProperties =
    [];

    private static readonly string[] MaterializeProjectionProperties =
    [
        nameof(CommandAction.MaterializeSourceObjectId)
    ];

    private static readonly string[] InvokeProcedureProjectionProperties =
    [
        nameof(CommandAction.ProcedureId)
    ];

    private static readonly string[] CompositeByTargetProjectionProperties =
    [
        nameof(CommandAction.CompositeTargetObjectId),
        nameof(CommandAction.CompositeRecipeId),
        nameof(CommandAction.CompositeRequiredPartObjectIds),
        nameof(CommandAction.CompositeStrictPartCountEnforcement),
        nameof(CommandAction.CompositeMinimumRequiredPartCount),
        nameof(CommandAction.CompositePartConsumptionMode)
    ];

    private static readonly string[] CompositeByPartsProjectionProperties =
    [
        nameof(CommandAction.CompositeTargetObjectId),
        nameof(CommandAction.CompositeRecipeId),
        nameof(CommandAction.CompositeRequiredPartObjectIds),
        nameof(CommandAction.CompositeStrictPartCountEnforcement),
        nameof(CommandAction.CompositeMinimumRequiredPartCount),
        nameof(CommandAction.CompositeMatchMode),
        nameof(CommandAction.CompositeAmbiguityPolicy),
        nameof(CommandAction.CompositePartConsumptionMode),
        nameof(CommandAction.CompositeResolvedTargetOutputTemplate)
    ];

    private static readonly string[] BreakCompositeProjectionProperties =
    [
        nameof(CommandAction.CompositeTargetObjectId),
        nameof(CommandAction.CompositeRecipeId),
        nameof(CommandAction.CompositeRequiredPartObjectIds),
        nameof(CommandAction.CompositeStrictPartCountEnforcement),
        nameof(CommandAction.CompositeMinimumRequiredPartCount),
        nameof(CommandAction.CompositePartConsumptionMode)
    ];

    public static IActionPayload CreateDefaultPayload(CommandActionType actionType)
    {
        return actionType switch
        {
            CommandActionType.LinkedActions => new LinkedFlowPayload(new List<LinkedActionReference>()),
            CommandActionType.Synonym => new SynonymPayload(null),
            CommandActionType.EchoMessage => new EchoPayload(),
            CommandActionType.SetFlag => new SetFlagPayload(string.Empty, false),
            CommandActionType.CheckGameProperty => new CheckGamePropertyPayload(string.Empty, true),
            CommandActionType.SetGameProperty => new SetGamePropertyPayload(string.Empty, string.Empty),
            CommandActionType.PutObjectInContainer => new ContainerTransferPayload(string.Empty),
            CommandActionType.RemoveObjectFromContainer => new ContainerTransferPayload(string.Empty),
            CommandActionType.MoveRoomObjectOnGrid => new MoveRoomObjectOnGridPayload(),
            CommandActionType.MoveRoomObjectByPoints => new MoveRoomObjectByPointsPayload(
                TargetResolutionIntent: "PrimarySelection",
                AllowPartialMove: false,
                AllowJumpOver: false,
                VisualTransitionHint: RuntimeMovementVisualTransitionHint.Medium,
                TravelVisualizationMode: RuntimeMovementTravelVisualizationMode.LegByLeg),
            CommandActionType.RotateRoomObjectOnGrid => new RotateRoomObjectOnGridPayload(),
            CommandActionType.StackRoomObjectOnAnother => new StackRoomObjectOnAnotherPayload(),
            CommandActionType.OpenObject => new EchoPayload(),
            CommandActionType.CloseObject => new EchoPayload(),
            CommandActionType.UnlockObject => new EchoPayload(),
            CommandActionType.LockObject => new EchoPayload(),
            CommandActionType.SetActiveRoomObject => new SetActiveRoomObjectPayload(string.Empty),
            CommandActionType.SelectRoomObjectByPoint => new SelectRoomObjectByPointPayload(string.Empty),
            CommandActionType.ClearActiveRoomObjects => new ClearActiveRoomObjectsPayload(RuntimeClearActiveRoomObjectsScope.Both),
            CommandActionType.ClearRoomObjectSelections => new ClearRoomObjectSelectionsPayload(ClearRoomObjectSelectionsScope.All),
            CommandActionType.StartTimer => new StartTimerPayload(string.Empty, null),
            CommandActionType.CancelTimer => new CancelTimerPayload(string.Empty, null, null),
            CommandActionType.NavigateDirection => new NavigatePayload(),
            CommandActionType.NavigateToAdjacent => new NavigatePayload(),
            CommandActionType.MaterializeObjectCopy => new MaterializeObjectCopyPayload(null),
            CommandActionType.InvokeProcedure => new InvokeProcedurePayload(null),
            CommandActionType.BuildCompositeByTarget => new CompositeByTargetPayload(null, null, new List<Guid>(), null, null, "ContainedInComposite"),
            CommandActionType.BuildCompositeByParts => new CompositeByPartsPayload(null, null, new List<Guid>(), null, null, string.Empty, string.Empty, "ContainedInComposite", string.Empty),
            CommandActionType.BreakCompositeItem => new BreakCompositePayload(null, null, new List<Guid>(), null, null, "ContainedInComposite"),
            CommandActionType.ManageNarrativePhaseAmbientSounds => new EchoPayload(),
            _ => new EchoPayload()
        };
    }

    public static IActionPayload? CreateSnapshot(
        CommandActionType actionType,
        IActionPayload payload,
        IReadOnlyList<LinkedActionReference> linkedActions)
    {
        if (actionType == CommandActionType.LinkedActions)
        {
            return new LinkedFlowPayload(CloneLinkedActions(linkedActions));
        }

        return ClonePayload(payload);
    }

    public static IActionPayload ClonePayload(IActionPayload payload)
    {
        return payload switch
        {
            LinkedFlowPayload linkedFlow => new LinkedFlowPayload(CloneLinkedActions(linkedFlow.LinkedActions)),
            SynonymPayload synonym => new SynonymPayload(synonym.SynonymTargetActionId),
            EchoPayload => new EchoPayload(),
            SetFlagPayload setFlag => new SetFlagPayload(setFlag.FlagName, setFlag.FlagValue),
            CheckGamePropertyPayload check => new CheckGamePropertyPayload(check.PropertyName, check.ExpectedValue),
            SetGamePropertyPayload set => new SetGamePropertyPayload(set.PropertyName, set.PropertyValue),
            ContainerTransferPayload transfer => new ContainerTransferPayload(transfer.TargetContainerId),
            MoveRoomObjectOnGridPayload move => new MoveRoomObjectOnGridPayload(
                move.DirectionToken,
                move.DistanceInCells,
                move.AllowPartialMove,
                move.AllowJumpOver,
                move.VisualTransitionHint,
                move.TravelVisualizationMode),
            MoveRoomObjectByPointsPayload moveByPoints => new MoveRoomObjectByPointsPayload(
                moveByPoints.TargetResolutionIntent,
                moveByPoints.AllowPartialMove,
                moveByPoints.AllowJumpOver,
                moveByPoints.VisualTransitionHint,
                moveByPoints.TravelVisualizationMode),
            RotateRoomObjectOnGridPayload rotate => new RotateRoomObjectOnGridPayload(
                rotate.Mode,
                rotate.TurnDegrees,
                rotate.FacingDirectionToken,
                rotate.VisualTransitionHint),
            StackRoomObjectOnAnotherPayload stack => new StackRoomObjectOnAnotherPayload(
                stack.VisualTransitionHint),
            SetActiveRoomObjectPayload setActive => new SetActiveRoomObjectPayload(
                setActive.SelectionCueEffectKey),
            SelectRoomObjectByPointPayload selectByPoint => new SelectRoomObjectByPointPayload(
                selectByPoint.SelectionCueEffectKey),
            ClearActiveRoomObjectsPayload clearActive => new ClearActiveRoomObjectsPayload(
                clearActive.ClearScope),
            ClearRoomObjectSelectionsPayload clearSelections => new ClearRoomObjectSelectionsPayload(
                clearSelections.ClearScope),
            StartTimerPayload startTimer => new StartTimerPayload(
                startTimer.TimerKey,
                startTimer.OwnerScopeKindOverride),
            CancelTimerPayload cancelTimer => new CancelTimerPayload(
                cancelTimer.TimerKey,
                cancelTimer.ScopeQualifierKind,
                cancelTimer.ScopeQualifierId),
            NavigatePayload => new NavigatePayload(),
            MaterializeObjectCopyPayload materialize => new MaterializeObjectCopyPayload(materialize.MaterializeSourceObjectId),
            InvokeProcedurePayload invokeProcedure => new InvokeProcedurePayload(invokeProcedure.ProcedureId),
            CompositeByTargetPayload byTarget => new CompositeByTargetPayload(
                byTarget.CompositeTargetObjectId,
                byTarget.CompositeRecipeId,
                byTarget.CompositeRequiredPartObjectIds.ToList(),
                byTarget.CompositeStrictPartCountEnforcement,
                byTarget.CompositeMinimumRequiredPartCount,
                byTarget.CompositePartConsumptionMode),
            CompositeByPartsPayload byParts => new CompositeByPartsPayload(
                byParts.CompositeTargetObjectId,
                byParts.CompositeRecipeId,
                byParts.CompositeRequiredPartObjectIds.ToList(),
                byParts.CompositeStrictPartCountEnforcement,
                byParts.CompositeMinimumRequiredPartCount,
                byParts.CompositeMatchMode,
                byParts.CompositeAmbiguityPolicy,
                byParts.CompositePartConsumptionMode,
                byParts.CompositeResolvedTargetOutputTemplate),
            BreakCompositePayload breakComposite => new BreakCompositePayload(
                breakComposite.CompositeTargetObjectId,
                breakComposite.CompositeRecipeId,
                breakComposite.CompositeRequiredPartObjectIds.ToList(),
                breakComposite.CompositeStrictPartCountEnforcement,
                breakComposite.CompositeMinimumRequiredPartCount,
                breakComposite.CompositePartConsumptionMode),
            _ => payload
        };
    }

    public static bool TryReadCompatibilityState(IActionPayload payload, out List<LinkedActionReference>? linkedActions)
    {
        linkedActions = null;

        if (payload is LinkedFlowPayload linkedFlow)
        {
            linkedActions = CloneLinkedActions(linkedFlow.LinkedActions);
            return true;
        }

        return false;
    }

    public static string GetCheckPropertyName(IActionPayload payload)
    {
        return payload switch
        {
            CheckGamePropertyPayload check => check.PropertyName ?? string.Empty,
            SetFlagPayload setFlag => setFlag.FlagName ?? string.Empty,
            _ => string.Empty
        };
    }

    public static bool GetCheckExpectedValue(IActionPayload payload)
    {
        return payload switch
        {
            CheckGamePropertyPayload check => check.ExpectedValue,
            SetFlagPayload setFlag => setFlag.FlagValue,
            _ => false
        };
    }

    public static bool TrySetCheckPropertyName(ref IActionPayload payload, string propertyName)
    {
        if (payload is CheckGamePropertyPayload check)
        {
            if (string.Equals(check.PropertyName, propertyName, StringComparison.Ordinal))
            {
                return false;
            }

            payload = check with { PropertyName = propertyName };
            return true;
        }

        if (payload is SetFlagPayload setFlag)
        {
            if (string.Equals(setFlag.FlagName, propertyName, StringComparison.Ordinal))
            {
                return false;
            }

            payload = setFlag with { FlagName = propertyName };
            return true;
        }

        return false;
    }

    public static bool TrySetCheckExpectedValue(ref IActionPayload payload, bool expectedValue)
    {
        if (payload is CheckGamePropertyPayload check)
        {
            if (check.ExpectedValue == expectedValue)
            {
                return false;
            }

            payload = check with { ExpectedValue = expectedValue };
            return true;
        }

        if (payload is SetFlagPayload setFlag)
        {
            if (setFlag.FlagValue == expectedValue)
            {
                return false;
            }

            payload = setFlag with { FlagValue = expectedValue };
            return true;
        }

        return false;
    }

    public static string GetSetPropertyName(IActionPayload payload)
    {
        return payload is SetGamePropertyPayload set
            ? set.PropertyName ?? string.Empty
            : string.Empty;
    }

    public static string GetSetPropertyValue(IActionPayload payload)
    {
        return payload is SetGamePropertyPayload set
            ? set.PropertyValue ?? string.Empty
            : string.Empty;
    }

    public static bool TrySetSetPropertyName(ref IActionPayload payload, string propertyName)
    {
        if (payload is not SetGamePropertyPayload set)
        {
            return false;
        }

        if (string.Equals(set.PropertyName, propertyName, StringComparison.Ordinal))
        {
            return false;
        }

        payload = set with { PropertyName = propertyName };
        return true;
    }

    public static bool TrySetSetPropertyValue(ref IActionPayload payload, string propertyValue)
    {
        if (payload is not SetGamePropertyPayload set)
        {
            return false;
        }

        if (string.Equals(set.PropertyValue, propertyValue, StringComparison.Ordinal))
        {
            return false;
        }

        payload = set with { PropertyValue = propertyValue };
        return true;
    }

    public static Guid? GetSynonymTargetActionId(IActionPayload payload)
    {
        return payload is SynonymPayload synonym ? synonym.SynonymTargetActionId : null;
    }

    public static bool TrySetSynonymTargetActionId(ref IActionPayload payload, Guid? targetActionId)
    {
        if (payload is not SynonymPayload synonym)
        {
            return false;
        }

        if (synonym.SynonymTargetActionId == targetActionId)
        {
            return false;
        }

        payload = synonym with { SynonymTargetActionId = targetActionId };
        return true;
    }

    public static Guid? GetMaterializeSourceObjectId(IActionPayload payload)
    {
        return payload is MaterializeObjectCopyPayload materialize
            ? materialize.MaterializeSourceObjectId
            : null;
    }

    public static bool TrySetMaterializeSourceObjectId(ref IActionPayload payload, Guid? sourceObjectId)
    {
        if (payload is not MaterializeObjectCopyPayload materialize)
        {
            return false;
        }

        if (materialize.MaterializeSourceObjectId == sourceObjectId)
        {
            return false;
        }

        payload = materialize with { MaterializeSourceObjectId = sourceObjectId };
        return true;
    }

    public static Guid? GetProcedureId(IActionPayload payload)
    {
        return payload is InvokeProcedurePayload invokeProcedure
            ? invokeProcedure.ProcedureId
            : null;
    }

    public static string GetStartTimerKey(IActionPayload payload)
    {
        return payload is StartTimerPayload startTimer
            ? startTimer.TimerKey ?? string.Empty
            : string.Empty;
    }

    public static TimerOwnerType? GetStartTimerOwnerScopeKindOverride(IActionPayload payload)
    {
        return payload is StartTimerPayload startTimer
            ? startTimer.OwnerScopeKindOverride
            : null;
    }

    public static bool TrySetStartTimer(ref IActionPayload payload, string timerKey, TimerOwnerType? ownerScopeKindOverride)
    {
        if (payload is not StartTimerPayload startTimer)
        {
            return false;
        }

        var normalizedTimerKey = timerKey?.Trim() ?? string.Empty;
        if (string.Equals(startTimer.TimerKey, normalizedTimerKey, StringComparison.Ordinal)
            && startTimer.OwnerScopeKindOverride == ownerScopeKindOverride)
        {
            return false;
        }

        payload = startTimer with
        {
            TimerKey = normalizedTimerKey,
            OwnerScopeKindOverride = ownerScopeKindOverride
        };

        return true;
    }

    public static string GetCancelTimerKey(IActionPayload payload)
    {
        return payload is CancelTimerPayload cancelTimer
            ? cancelTimer.TimerKey ?? string.Empty
            : string.Empty;
    }

    public static TimerOwnerType? GetCancelTimerScopeQualifierKind(IActionPayload payload)
    {
        return payload is CancelTimerPayload cancelTimer
            ? cancelTimer.ScopeQualifierKind
            : null;
    }

    public static Guid? GetCancelTimerScopeQualifierId(IActionPayload payload)
    {
        return payload is CancelTimerPayload cancelTimer
            ? cancelTimer.ScopeQualifierId
            : null;
    }

    public static bool TrySetCancelTimer(ref IActionPayload payload, string timerKey, TimerOwnerType? scopeQualifierKind, Guid? scopeQualifierId)
    {
        if (payload is not CancelTimerPayload cancelTimer)
        {
            return false;
        }

        var normalizedTimerKey = timerKey?.Trim() ?? string.Empty;
        if (string.Equals(cancelTimer.TimerKey, normalizedTimerKey, StringComparison.Ordinal)
            && cancelTimer.ScopeQualifierKind == scopeQualifierKind
            && cancelTimer.ScopeQualifierId == scopeQualifierId)
        {
            return false;
        }

        payload = cancelTimer with
        {
            TimerKey = normalizedTimerKey,
            ScopeQualifierKind = scopeQualifierKind,
            ScopeQualifierId = scopeQualifierId
        };

        return true;
    }

    public static bool TrySetProcedureId(ref IActionPayload payload, Guid? procedureId)
    {
        if (payload is not InvokeProcedurePayload invokeProcedure)
        {
            return false;
        }

        if (invokeProcedure.ProcedureId == procedureId)
        {
            return false;
        }

        payload = invokeProcedure with { ProcedureId = procedureId };
        return true;
    }

    public static IReadOnlyList<string> GetProjectionPropertyNames(IActionPayload payload)
    {
        return payload switch
        {
            EchoPayload => EchoProjectionProperties,
            LinkedFlowPayload => LinkedProjectionProperties,
            SynonymPayload => SynonymProjectionProperties,
            SetFlagPayload => SetFlagProjectionProperties,
            CheckGamePropertyPayload => CheckProjectionProperties,
            SetGamePropertyPayload => SetProjectionProperties,
            ContainerTransferPayload => ContainerProjectionProperties,
            MoveRoomObjectOnGridPayload => MoveProjectionProperties,
            MoveRoomObjectByPointsPayload => MoveByPointsProjectionProperties,
            RotateRoomObjectOnGridPayload => RotateProjectionProperties,
            StackRoomObjectOnAnotherPayload => StackProjectionProperties,
            SetActiveRoomObjectPayload => SetActiveRoomObjectProjectionProperties,
            SelectRoomObjectByPointPayload => SelectRoomObjectByPointProjectionProperties,
            ClearActiveRoomObjectsPayload => ClearActiveRoomObjectsProjectionProperties,
            ClearRoomObjectSelectionsPayload => [],
            StartTimerPayload => StartTimerProjectionProperties,
            CancelTimerPayload => CancelTimerProjectionProperties,
            NavigatePayload => NavigateProjectionProperties,
            MaterializeObjectCopyPayload => MaterializeProjectionProperties,
            InvokeProcedurePayload => InvokeProcedureProjectionProperties,
            CompositeByTargetPayload => CompositeByTargetProjectionProperties,
            CompositeByPartsPayload => CompositeByPartsProjectionProperties,
            BreakCompositePayload => BreakCompositeProjectionProperties,
            _ => []
        };
    }

    private static List<LinkedActionReference> CloneLinkedActions(IEnumerable<LinkedActionReference> links)
    {
        return links.Select(static link => new LinkedActionReference
        {
            ActionId = link.ActionId,
            RunWhen = link.RunWhen,
            Order = link.Order
        }).ToList();
    }
}
