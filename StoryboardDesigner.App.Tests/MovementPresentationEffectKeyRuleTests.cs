using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Rules.Actions;

namespace StoryboardDesigner.App.Tests;

public sealed class MovementPresentationEffectKeyRuleTests
{
    [Fact]
    public void Evaluate_RejectsMissingOrNonMovementCatalogKeys()
    {
        var rule = new MovementPresentationEffectKeyRule();
        var action = new CommandAction
        {
            Name = "Move",
            ActionType = CommandActionType.MoveRoomObjectOnGrid
        };
        ActionPayloadAccessors.SetMoveRoomObjectOnGrid(action, "north", 1, false, "appearance.selection.outline.cyan");
        var context = CreateCandidate(action);

        var issue = Assert.Single(rule.Evaluate(context));

        Assert.Equal("ACT-017", issue.RuleId);

        ActionPayloadAccessors.SetMoveRoomObjectOnGrid(action, "north", 1, false, "unknown.effect.key");
        Assert.Single(rule.Evaluate(context));
    }

    [Fact]
    public void Evaluate_AllowsConfiguredMovementEffectKey()
    {
        var rule = new MovementPresentationEffectKeyRule();
        var action = new CommandAction
        {
            Name = "Move",
            ActionType = CommandActionType.MoveRoomObjectOnGrid
        };
        ActionPayloadAccessors.SetMoveRoomObjectOnGrid(action, "north", 1, false, "movement.speed.medium");
        var context = CreateCandidate(action);

        Assert.Empty(rule.Evaluate(context));
    }

    private static ValidationRuleContext CreateCandidate(CommandAction action)
    {
        var room = new Room { Name = "Room", AvailableActions = [action] };
        var project = new ProjectModel();
        var context = ValidationRuleTestContext.Create(project) with
        {
            CandidateNode = new ValidationActionCandidateNode(action, room, "Project / Room")
        };
        return context;
    }
}
