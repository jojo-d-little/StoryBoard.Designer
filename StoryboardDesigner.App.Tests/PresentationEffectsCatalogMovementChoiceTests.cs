using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class PresentationEffectsCatalogMovementChoiceTests
{
    [Fact]
    public void GetConfiguredMovementEffects_UsesMovementCatalogKeysAndDisplayNames()
    {
        var service = new PresentationEffectsCatalogService();

        var choices = service.GetConfiguredMovementEffects();

        Assert.Contains(choices, choice => choice.EffectKey == "movement.speed.quantum" && choice.DisplayName == "Quantum");
        Assert.Contains(choices, choice => choice.EffectKey == "movement.speed.slow" && choice.DisplayName == "Slow");
        Assert.Contains(choices, choice => choice.EffectKey == "movement.speed.medium" && choice.DisplayName == "Medium");
        Assert.Contains(choices, choice => choice.EffectKey == "movement.speed.fast" && choice.DisplayName == "Fast");
        Assert.All(choices, choice => Assert.True(service.IsConfiguredMovementEffectKey(choice.EffectKey)));
    }

    [Fact]
    public void GetDefaultMovementEffectKey_SelectsConfiguredMediumEffectKey()
    {
        var service = new PresentationEffectsCatalogService();

        Assert.Equal("movement.speed.medium", service.GetDefaultMovementEffectKey());
    }

    [Fact]
    public void DefaultMovePayload_UsesMediumEffectKey()
    {
        var action = new CommandAction { ActionType = CommandActionType.MoveRoomObjectOnGrid };

        Assert.Equal("movement.speed.medium", ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(action).MovementEffectKey);
        Assert.Equal("movement.speed.medium", ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(
            new CommandAction { ActionType = CommandActionType.MoveRoomObjectByPoints }).MovementEffectKey);
        Assert.Equal("movement.speed.medium", ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(
            new CommandAction { ActionType = CommandActionType.RotateRoomObjectOnGrid }).MovementEffectKey);
        Assert.Equal("movement.speed.medium", ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(
            new CommandAction { ActionType = CommandActionType.StackRoomObjectOnAnother }).MovementEffectKey);
    }
}
