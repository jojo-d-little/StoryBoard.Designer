using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceTraversalValidationTests
{
    [Fact]
    public void SaveProjectModel_ThrowsActionableValidationError_ForInvalidTraversalData()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room { Name = "A" };
            var roomB = new Room { Name = "B" };

            var area = new Area
            {
                Name = "Area",
                Rooms = [roomA, roomB],
                TraversalConnections =
                [
                    new TraversalConnection
                    {
                        RoomAId = roomA.Id,
                        RoomBId = roomB.Id,
                        TraversalModeOverride = AreaAdjacencyMode.FourDirectional,
                        BaseTraversalDirectionFromA = Direction10.NorthEast,
                        TraversalStateFromA = BuildPassableLeg("true"),
                        TraversalStateFromB = BuildPassableLeg("true")
                    }
                ]
            };

            var project = BuildProject(area);
            var projectFilePath = Path.Combine(tempRoot, "InvalidTraversalSave.sbe.json");

            var ex = Assert.Throws<InvalidOperationException>(() => service.SaveProjectModel(projectFilePath, project));
            Assert.Contains("Traversal validation failed during save", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("diagonal", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Fix:", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static TraversalLegState BuildPassableLeg(string defaultValue)
    {
        return new TraversalLegState
        {
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPassable",
                    DefaultValue = defaultValue,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };
    }

    private static ProjectModel BuildProject(Area area)
    {
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };

        return new ProjectModel
        {
            Name = "TraversalValidationProject",
            Planets = [planet]
        };
    }
}
