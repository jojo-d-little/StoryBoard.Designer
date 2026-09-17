using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalWizardApplyUtilityTests
{
    [Fact]
    public void BuildUniqueDoorName_UsesUnderscoreSuffix_ForCollisions()
    {
        var roomObjects = new List<GameObject>
        {
            new() { Name = "Door_N" },
            new() { Name = "Door_N_2" },
            new() { Name = "door_n_3" }
        };

        var name = TraversalWizardApplyUtility.BuildUniqueDoorName(roomObjects, Direction.North);

        Assert.Equal("Door_N_4", name);
    }

    [Fact]
    public void BuildDoorProducerNotes_UsesLockedContractFormat()
    {
        var notes = TraversalWizardApplyUtility.BuildDoorProducerNotes(Direction.SouthWest, "Atrium", "Gallery");

        Assert.Equal("Door opens SouthWest from Atrium to Gallery.", notes);
    }

    [Fact]
    public void BuildTraversalLookEchoMessage_UsesLockedContractText()
    {
        var message = TraversalWizardApplyUtility.BuildTraversalLookEchoMessage("Kitchen");

        Assert.Equal("There looks to be a Kitchen thru the door", message);
    }

    [Theory]
    [InlineData(Direction.North, 0)]
    [InlineData(Direction.East, 90)]
    [InlineData(Direction.South, 180)]
    [InlineData(Direction.West, 270)]
    public void ResolveDoorRotationDegrees_UsesCardinalWallOffsets(Direction direction, double expectedDegrees)
    {
        var degrees = TraversalWizardApplyUtility.ResolveDoorRotationDegrees(direction);

        Assert.Equal(expectedDegrees, degrees);
    }

    [Theory]
    [InlineData(Direction.North, 452, 0)]
    [InlineData(Direction.East, 904, 302)]
    [InlineData(Direction.South, 452, 604)]
    [InlineData(Direction.West, 0, 302)]
    public void ResolveDoorWallCenterPosition_AlignsTopLeftToWallCenterUsingNominalDoorFootprint(Direction direction, double expectedX, double expectedY)
    {
        var position = TraversalWizardApplyUtility.ResolveDoorWallCenterPosition(direction, 1000, 700);

        Assert.Equal(expectedX, position.X);
        Assert.Equal(expectedY, position.Y);
    }

    [Theory]
    [InlineData(Direction.North, 436, 0)]
    [InlineData(Direction.East, 872, 270)]
    [InlineData(Direction.West, 0, 270)]
    public void ResolveDoorWallCenterPosition_UsesProvidedFootprintForCentering(Direction direction, double expectedX, double expectedY)
    {
        var position = TraversalWizardApplyUtility.ResolveDoorWallCenterPosition(
            direction,
            canvasWidth: 1000,
            canvasHeight: 700,
            footprintWidth: 128,
            footprintHeight: 160);

        Assert.Equal(expectedX, position.X);
        Assert.Equal(expectedY, position.Y);
    }

    [Fact]
    public void ResolveRotatedFootprint_AtNinetyDegrees_SwapsWidthAndHeight()
    {
        var rotated = TraversalWizardApplyUtility.ResolveRotatedFootprint(120, 60, 90);

        Assert.Equal(60, rotated.Width, 3);
        Assert.Equal(120, rotated.Height, 3);
    }

    [Fact]
    public void ResolveRotatedFootprint_AtFortyFiveDegrees_ExpandsBounds()
    {
        var rotated = TraversalWizardApplyUtility.ResolveRotatedFootprint(100, 50, 45);

        Assert.Equal(106.066, rotated.Width, 3);
        Assert.Equal(106.066, rotated.Height, 3);
    }
}
