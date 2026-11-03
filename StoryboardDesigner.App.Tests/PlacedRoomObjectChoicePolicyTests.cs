using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class PlacedRoomObjectChoicePolicyTests
{
    [Fact]
    public void CaptureTargetChoices_IncludePlacedSequenceOwnerAndItsChildren_NotBaseDefinitions()
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
        var room = new Room { Name = "Elevator Foyer", GameObjects = [placedElevator] };
        var area = new Area { Name = "Area", BaseObjects = [baseElevator], Rooms = [room] };
        var project = new ProjectModel
        {
            Name = "Capture Choices",
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [area] }] }]
        };
        ScopeHierarchy.AttachParents(project);

        var choices = new[]
        {
            BuildChoice(baseElevator),
            BuildChoice(baseFloor),
            BuildChoice(placedElevator),
            BuildChoice(placedFloor)
        };

        var eligible = PlacedRoomObjectChoicePolicy.Filter(choices);

        Assert.Equal(
            new[] { placedElevator.ObjectId, placedFloor.ObjectId }.OrderBy(static id => id),
            eligible.Select(static choice => choice.ObjectId).OrderBy(static id => id));
    }

    private static MaterializeSourceObjectChoiceItem BuildChoice(GameObject gameObject) => new()
    {
        ObjectId = gameObject.ObjectId,
        SourceObject = gameObject,
        DisplayName = gameObject.Name,
        SourceCategory = "Current Object"
    };
}
