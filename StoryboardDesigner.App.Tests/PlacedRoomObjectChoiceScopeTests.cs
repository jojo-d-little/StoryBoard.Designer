using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class CaptureObjectTargetChoiceScopeTests
{
    [Fact]
    public void BaseObjectSequenceChoices_ShowOnlyCurrentObjectAndItsChildren()
    {
        var baseFloor = new GameObject { Name = "TheFloor" };
        var baseElevator = new GameObject { Name = "Elevator", ContainedObjects = [baseFloor] };
        var placedFloor = new GameObject { Name = "TheFloor", LinkedBaseObjectId = baseFloor.ObjectId };
        var placedElevator = new GameObject
        {
            Name = "Elevator",
            LinkedBaseObjectId = baseElevator.ObjectId,
            ContainedObjects = [placedFloor]
        };
        var unrelatedPlacement = new GameObject { Name = "Lamp", LinkedBaseObjectId = Guid.NewGuid() };
        var room = new Room { Name = "3rd Elevator Foyer", GameObjects = [placedElevator, unrelatedPlacement] };
        var area = new Area { Name = "Birmingham", BaseObjects = [baseElevator], Rooms = [room] };
        var project = new ProjectModel
        {
            Name = "Capture target scope",
            Planets = [new Planet { Name = "Earth", Countries = [new Country { Name = "England", Areas = [area] }] }]
        };
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetMaterializeSourceObjectChoicesForScopeNode(baseElevator);
        var eligibleTargets = PlacedRoomObjectChoicePolicy.Filter(choices, baseElevator.ObjectId);

        Assert.Equal(
            new[] { baseElevator.ObjectId, baseFloor.ObjectId }.OrderBy(static id => id),
            eligibleTargets.Select(static choice => choice.ObjectId).OrderBy(static id => id));
        Assert.DoesNotContain(eligibleTargets, choice => choice.ObjectId == placedElevator.ObjectId);
        Assert.DoesNotContain(eligibleTargets, choice => choice.ObjectId == placedFloor.ObjectId);
    }
}
