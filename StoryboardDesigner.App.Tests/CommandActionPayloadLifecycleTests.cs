using StoryboardDesigner.App.Models;
using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Tests;

public sealed class CommandActionPayloadLifecycleTests
{
    [Fact]
    public void CreatePayloadSnapshot_ReturnsEchoPayload_ForEchoAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.EchoMessage,
            EchoMessage = "Hello world",
            Payload = new StoryboardDesigner.App.Models.EchoPayload(),
        };

        var payload = action.CreatePayloadSnapshot();

        Assert.IsType<EchoPayload>(payload);
        Assert.Equal("Hello world", ActionPayloadAccessors.GetEchoMessage(action));
    }

    [Fact]
    public void TryApplyPayload_UpdatesActionFields_ForSetGamePropertyAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetGameProperty,
            GamePropertyName = "OldName",
            GamePropertyValue = "OldValue"
        };

        var applied = action.TryApplyPayload(new SetGamePropertyPayload("DoorOpen", "true"));

        Assert.True(applied);
        Assert.Equal("DoorOpen", action.GamePropertyName);
        Assert.Equal("true", action.GamePropertyValue);
        Assert.IsType<SetGamePropertyPayload>(action.Payload);
    }

    [Fact]
    public void TryApplyPayload_ReturnsFalse_WhenPayloadTypeDoesNotMatchActionType_AndLeavesCurrentPayloadProjectionUnchanged()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.EchoMessage,
            EchoMessage = "unchanged",
            Payload = new StoryboardDesigner.App.Models.EchoPayload(),
        };

        var applied = action.TryApplyPayload(new SetGamePropertyPayload("Prop", "Value"));

        Assert.False(applied);
        Assert.Equal("unchanged", ActionPayloadAccessors.GetEchoMessage(action));
        Assert.IsType<EchoPayload>(action.Payload);
    }

    [Fact]
    public void CreatePayloadSnapshot_CapturesLinkedActionsSnapshot()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.LinkedActions
        };

        action.LinkedActions.Add(new LinkedActionReference
        {
            ActionId = Guid.NewGuid(),
            RunWhen = LinkedActionRunWhen.OnSuccess,
            Order = 2
        });

        action.Payload = action.CreatePayloadSnapshot();

        var payload = Assert.IsType<LinkedFlowPayload>(action.Payload);
        Assert.Single(payload.LinkedActions);
        Assert.Equal(2, payload.LinkedActions[0].Order);
        Assert.Equal(LinkedActionRunWhen.OnSuccess, payload.LinkedActions[0].RunWhen);
    }

    [Fact]
    public void Accessors_GetEchoMessage_ReadsPayloadWhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.EchoMessage,
            EchoMessage = "fallback",
            Payload = new StoryboardDesigner.App.Models.EchoPayload(),
        };

        action.EchoMessage = "payload";
        action.Payload = new EchoPayload();

        var value = ActionPayloadAccessors.GetEchoMessage(action);

        Assert.Equal("payload", value);
    }

    [Fact]
    public void Accessors_SetCheckGameProperty_UpdatesActionFieldsAndPayloadForCheckAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.CheckGameProperty
        };

        ActionPayloadAccessors.SetCheckGameProperty(action, "doorOpen", true);

        Assert.Equal("doorOpen", action.FlagName);
        Assert.True(action.FlagValue);
        var payload = Assert.IsType<CheckGamePropertyPayload>(action.Payload);
        Assert.Equal("doorOpen", payload.PropertyName);
        Assert.True(payload.ExpectedValue);
    }

    [Fact]
    public void Accessors_SetSetGameProperty_UpdatesActionFieldsAndPayloadForSetAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetGameProperty
        };

        ActionPayloadAccessors.SetSetGameProperty(action, "currentRoom.isLit", "true");

        Assert.Equal("currentRoom.isLit", action.GamePropertyName);
        Assert.Equal("true", action.GamePropertyValue);
        var payload = Assert.IsType<SetGamePropertyPayload>(action.Payload);
        Assert.Equal("currentRoom.isLit", payload.PropertyName);
        Assert.Equal("true", payload.PropertyValue);
    }

    [Fact]
    public void CreatePayloadSnapshot_ReturnsSetFlagPayload_ForSetFlagAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetFlag,
            FlagName = "doorUnlocked",
            FlagValue = true
        };

        var payload = action.CreatePayloadSnapshot();

        var setFlag = Assert.IsType<SetFlagPayload>(payload);
        Assert.Equal("doorUnlocked", setFlag.FlagName);
        Assert.True(setFlag.FlagValue);
    }

    [Fact]
    public void Accessors_SetSetFlag_UpdatesActionFieldsAndPayloadForSetFlagAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetFlag
        };

        ActionPayloadAccessors.SetSetFlag(action, "HasLantern", true);

        Assert.Equal("HasLantern", action.FlagName);
        Assert.True(action.FlagValue);
        var payload = Assert.IsType<SetFlagPayload>(action.Payload);
        Assert.Equal("HasLantern", payload.FlagName);
        Assert.True(payload.FlagValue);
    }

    [Fact]
    public void Accessors_GetSetFlagNameAndValue_ReturnActionFields()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetFlag,
            FlagName = "HasLantern",
            FlagValue = true
        };

        var flagName = ActionPayloadAccessors.GetSetFlagName(action);
        var flagValue = ActionPayloadAccessors.GetSetFlagValue(action);

        Assert.Equal("HasLantern", flagName);
        Assert.True(flagValue);
    }

    [Fact]
    public void Accessors_GetSetFlagNameAndValue_DefaultWhenUnset()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetFlag
        };

        var flagName = ActionPayloadAccessors.GetSetFlagName(action);
        var flagValue = ActionPayloadAccessors.GetSetFlagValue(action);

        Assert.Equal(string.Empty, flagName);
        Assert.False(flagValue);
    }

    [Fact]
    public void GetReferenceTokens_ReturnsNavigateTokens_FromPayload()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.NavigateDirection,
            Payload = new NavigatePayload()
        };

        var tokens = action.GetReferenceTokens();

        Assert.Contains("currentAction.success", tokens);
        Assert.Contains("currentAction.resultCode", tokens);
        Assert.Contains("currentAction.priorRoomName", tokens);
        Assert.Contains("currentAction.destinationRoomName", tokens);
    }

    [Fact]
    public void GetReferenceTokens_ReturnsBreakCompositeTokens_FromPayload()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.BreakCompositeItem,
            Payload = new BreakCompositePayload(
                CompositeTargetObjectId: null,
                CompositeRecipeId: null,
                CompositeRequiredPartObjectIds: [],
                CompositeStrictPartCountEnforcement: null,
                CompositeMinimumRequiredPartCount: null,
                CompositePartConsumptionMode: string.Empty)
        };

        var tokens = action.GetReferenceTokens();

        Assert.Contains("currentAction.breakTarget", tokens);
        Assert.Contains("currentAction.returnedParts", tokens);
        Assert.Contains("currentAction.restoredParts", tokens);
    }

    [Fact]
    public void Accessors_SetMoveRoomObjectOnGrid_UpdatesPayloadForMoveAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.MoveRoomObjectOnGrid
        };

        ActionPayloadAccessors.SetMoveRoomObjectOnGrid(
            action,
            "north",
            3,
            true,
            RuntimeMovementVisualTransitionHint.Fast,
            allowJumpOver: true);

        var payload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(action);
        Assert.Equal("north", payload.DirectionToken);
        Assert.Equal(3, payload.DistanceInCells);
        Assert.True(payload.AllowPartialMove);
        Assert.True(payload.AllowJumpOver);
        Assert.Equal(RuntimeMovementVisualTransitionHint.Fast, payload.VisualTransitionHint);
    }

    [Fact]
    public void Accessors_SetRotateRoomObjectOnGrid_UpdatesPayloadForRotateAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.RotateRoomObjectOnGrid
        };

        ActionPayloadAccessors.SetRotateRoomObjectOnGrid(
            action,
            RuntimeRotateRoomObjectOnGridAttemptMode.Face,
            null,
            "east",
            RuntimeMovementVisualTransitionHint.Slow);

        var payload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(action);
        Assert.Equal(RuntimeRotateRoomObjectOnGridAttemptMode.Face, payload.Mode);
        Assert.Null(payload.TurnDegrees);
        Assert.Equal("E", payload.FacingDirectionToken);
        Assert.Equal(RuntimeMovementVisualTransitionHint.Slow, payload.VisualTransitionHint);
    }

    [Fact]
    public void CreatePayloadSnapshot_ReturnsPointActionPayloadTypes_ForPointCommandActions()
    {
        var selectAction = new CommandAction
        {
            ActionType = CommandActionType.SelectRoomObjectByPoint
        };

        var moveByPointsAction = new CommandAction
        {
            ActionType = CommandActionType.MoveRoomObjectByPoints
        };

        var clearSelectionsAction = new CommandAction
        {
            ActionType = CommandActionType.ClearRoomObjectSelections
        };

        Assert.IsType<SelectRoomObjectByPointPayload>(selectAction.CreatePayloadSnapshot());
        Assert.IsType<MoveRoomObjectByPointsPayload>(moveByPointsAction.CreatePayloadSnapshot());
        Assert.IsType<ClearRoomObjectSelectionsPayload>(clearSelectionsAction.CreatePayloadSnapshot());
    }

    [Fact]
    public void Accessors_SetStackRoomObjectOnAnother_UpdatesPayloadForStackAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.StackRoomObjectOnAnother
        };

        ActionPayloadAccessors.SetStackRoomObjectOnAnother(
            action,
            RuntimeMovementVisualTransitionHint.Fast);

        var payload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(action);
        Assert.Equal(RuntimeMovementVisualTransitionHint.Fast, payload.VisualTransitionHint);
    }

    [Fact]
    public void GetReferenceTokens_ReturnsEmpty_ForPayloadWithoutCustomTokens()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.EchoMessage,
            EchoMessage = "hello",
            Payload = new EchoPayload()
        };

        var tokens = action.GetReferenceTokens();

        Assert.Empty(tokens);
    }

    [Fact]
    public void Accessors_SetStartTimer_UpdatesPayloadForStartTimerAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.StartTimer
        };

        ActionPayloadAccessors.SetStartTimer(action, "door-close", TimerOwnerType.Room);

        var payload = Assert.IsType<StartTimerPayload>(action.Payload);
        Assert.Equal("door-close", payload.TimerKey);
        Assert.Equal(TimerOwnerType.Room, payload.OwnerScopeKindOverride);
        Assert.Equal("door-close", ActionPayloadAccessors.GetStartTimerKey(action));
        Assert.Equal(TimerOwnerType.Room, ActionPayloadAccessors.GetStartTimerOwnerScopeKindOverride(action));
    }

    [Fact]
    public void Accessors_SetCancelTimer_UpdatesPayloadForCancelTimerAction()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.CancelTimer
        };

        var scopeId = Guid.NewGuid();
        ActionPayloadAccessors.SetCancelTimer(action, "door-close", TimerOwnerType.Room, scopeId);

        var payload = Assert.IsType<CancelTimerPayload>(action.Payload);
        Assert.Equal("door-close", payload.TimerKey);
        Assert.Equal(TimerOwnerType.Room, payload.ScopeQualifierKind);
        Assert.Equal(scopeId, payload.ScopeQualifierId);
        Assert.Equal("door-close", ActionPayloadAccessors.GetCancelTimerKey(action));
        Assert.Equal(TimerOwnerType.Room, ActionPayloadAccessors.GetCancelTimerScopeQualifierKind(action));
        Assert.Equal(scopeId, ActionPayloadAccessors.GetCancelTimerScopeQualifierId(action));
    }
}

