using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Tests;

public sealed class CommandActionSummaryPresenterTests
{
    [Fact]
    public void BuildSummary_UsesActionEchoValue_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.EchoMessage,
            EchoMessage = "fallback",
            Payload = new StoryboardDesigner.App.Models.EchoPayload(),
        };

        action.EchoMessage = "payload line 1\npayload line 2";
        action.Payload = new EchoPayload();

        var summary = CommandActionSummaryPresenter.BuildSummary(action);
        var preview = CommandActionSummaryPresenter.BuildSummaryPreview(action);

        Assert.Contains("payload line 1\npayload line 2", summary, StringComparison.Ordinal);
        Assert.StartsWith("payload line 1", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSummary_UsesPayloadCheckGamePropertyValues_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.CheckGameProperty,
            FlagName = "fallbackFlag",
            FlagValue = false,
            Verbs = ["inspect"]
        };

        action.Payload = new CheckGamePropertyPayload("payloadFlag", true);

        var summary = CommandActionSummaryPresenter.BuildSummary(action);

        Assert.Contains("payloadFlag == True", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSummary_UsesPayloadSetFlagValues_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetFlag,
            FlagName = "fallbackFlag",
            FlagValue = false,
            Verbs = ["set"]
        };

        action.Payload = new SetFlagPayload("payloadSetFlag", true);

        var summary = CommandActionSummaryPresenter.BuildSummary(action);

        Assert.Contains("payloadSetFlag = True", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSummaryTooltip_ReturnsFullPayloadEcho_ForEchoActions()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.EchoMessage,
            EchoMessage = "fallback",
            Payload = new StoryboardDesigner.App.Models.EchoPayload(),
        };

        action.EchoMessage = "payload tooltip text";
        action.Payload = new EchoPayload();

        var tooltip = CommandActionSummaryPresenter.BuildSummaryTooltip(action);

        Assert.Equal("payload tooltip text", tooltip);
    }

    [Fact]
    public void BuildSummary_UsesPayloadContainerTransferValues_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.PutObjectInContainer,
            TargetContainerId = "fallbackContainer",
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        action.TargetContainerId = "payloadContainer";
        action.SetOutcomeScript("Success", "ok");
        action.SetOutcomeScript("Failure", "fail");
        action.Payload = new ContainerTransferPayload("payloadContainer");

        var summary = CommandActionSummaryPresenter.BuildSummary(action);

        Assert.Contains("Put in -> payloadContainer", summary, StringComparison.Ordinal);
        Assert.Contains("success msg: configured", summary, StringComparison.Ordinal);
        Assert.Contains("failure msg: configured", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSummary_UsesPayloadCompositeByPartsValues_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeMatchMode = "fallbackMode",
            CompositeAmbiguityPolicy = "fallbackAmbiguity"
        };

        action.Payload = new CompositeByPartsPayload(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [Guid.NewGuid(), Guid.NewGuid()],
            true,
            2,
            "payloadMode",
            "payloadAmbiguity",
            "payloadConsumption",
            string.Empty);

        var summary = CommandActionSummaryPresenter.BuildSummary(action);

        Assert.Contains("mode: payloadMode", summary, StringComparison.Ordinal);
        Assert.Contains("ambiguity: payloadAmbiguity", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSummary_UsesPayloadMoveRoomObjectOnGridValues_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.MoveRoomObjectOnGrid
        };

        ActionPayloadAccessors.SetMoveRoomObjectOnGrid(
            action,
            "west",
            4,
            true,
            RuntimeMovementVisualTransitionHint.Slow,
            allowJumpOver: true);

        var summary = CommandActionSummaryPresenter.BuildSummary(action);

        Assert.Contains("Move -> dir: west; cells: 4; allow partial; jump-over path; hint: Slow", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSummary_UsesPayloadRotateRoomObjectOnGridValues_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.RotateRoomObjectOnGrid
        };

        ActionPayloadAccessors.SetRotateRoomObjectOnGrid(
            action,
            RuntimeRotateRoomObjectOnGridAttemptMode.Face,
            null,
            "NW",
            RuntimeMovementVisualTransitionHint.Fast);

        var summary = CommandActionSummaryPresenter.BuildSummary(action);

        Assert.Contains("Rotate -> mode: Face; facing: NW; hint: Fast", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSummary_UsesPayloadStackRoomObjectOnAnotherValues_WhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.StackRoomObjectOnAnother
        };

        ActionPayloadAccessors.SetStackRoomObjectOnAnother(
            action,
            RuntimeMovementVisualTransitionHint.Quantum);

        var summary = CommandActionSummaryPresenter.BuildSummary(action);

        Assert.Contains("Stack ->", summary, StringComparison.Ordinal);
        Assert.Contains("hint: Quantum", summary, StringComparison.Ordinal);
    }
}

