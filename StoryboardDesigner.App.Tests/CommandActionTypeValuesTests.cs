using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class CommandActionTypeValuesTests
{
    [Fact]
    public void All_IncludesNavigateToAdjacent()
    {
        Assert.Contains(CommandActionType.NavigateToAdjacent, CommandActionTypeValues.All);
    }

    [Fact]
    public void LinkedFlowAllowed_IncludesNavigateToAdjacent()
    {
        Assert.Contains(CommandActionType.NavigateToAdjacent, CommandActionTypeValues.LinkedFlowAllowed);
    }

    [Fact]
    public void All_IncludesSetActiveRoomObject()
    {
        Assert.Contains(CommandActionType.SetActiveRoomObject, CommandActionTypeValues.All);
    }

    [Fact]
    public void LinkedFlowAllowed_IncludesSetActiveRoomObject()
    {
        Assert.Contains(CommandActionType.SetActiveRoomObject, CommandActionTypeValues.LinkedFlowAllowed);
    }

    [Fact]
    public void All_IncludesPointCommandActionTypes()
    {
        Assert.Contains(CommandActionType.SelectRoomObjectByPoint, CommandActionTypeValues.All);
        Assert.Contains(CommandActionType.MoveRoomObjectByPoints, CommandActionTypeValues.All);
        Assert.Contains(CommandActionType.ClearRoomObjectSelections, CommandActionTypeValues.All);
    }

    [Fact]
    public void LinkedFlowAllowed_IncludesPointCommandActionTypes()
    {
        Assert.Contains(CommandActionType.SelectRoomObjectByPoint, CommandActionTypeValues.LinkedFlowAllowed);
        Assert.Contains(CommandActionType.MoveRoomObjectByPoints, CommandActionTypeValues.LinkedFlowAllowed);
        Assert.Contains(CommandActionType.ClearRoomObjectSelections, CommandActionTypeValues.LinkedFlowAllowed);
    }
}
