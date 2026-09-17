using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelCompositeRecipeChoiceTests
{
    [Fact]
    public void GetCompositeRecipeChoicesForScopeNode_ResolvesRequiredParts_WhenStoredAsObjectIds()
    {
        var partA = new GameObject
        {
            Name = "Cloth",
            IsInventoriable = true,
            CompositeRecipeId = Guid.NewGuid()
        };

        var partB = new GameObject
        {
            Name = "Oil",
            IsInventoriable = true,
            CompositeRecipeId = Guid.NewGuid()
        };

        var target = new GameObject
        {
            Name = "Torch",
            IsCompositeTarget = true,
            CompositeRecipeId = Guid.NewGuid(),
            CompositeRequiredParts =
            [
                new CompositePartRequirement { PartObjectId = partA.ObjectId, RequiredQuantity = 1 },
                new CompositePartRequirement { PartObjectId = partB.ObjectId, RequiredQuantity = 1 }
            ]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [target, partA, partB]
        };

        var project = CreateProjectWithRoom(room);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetCompositeRecipeChoicesForScopeNode(room);

        var choice = Assert.Single(choices);
        Assert.Equal(target.CompositeRecipeId, choice.RecipeId);
        Assert.Equal(2, choice.RequiredPartObjectIds.Count);
        Assert.All(choice.RequiredPartObjectIds, id => Assert.NotEqual(Guid.Empty, id));
        Assert.Contains("Cloth", choice.RequiredPartsDisplay, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Oil", choice.RequiredPartsDisplay, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectModel CreateProjectWithRoom(Room room)
    {
        return new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Planet A",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country A",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area A",
                                    Rooms = [room],
                                    StartingRoomId = room.Id
                                }
                            ],
                            StartingAreaName = "Area A"
                        }
                    ],
                    StartingCountryName = "Country A"
                }
            ],
            StartingPlanetName = "Planet A"
        };
    }

}
