using System.Text.Json;
using System.Text.Json.Nodes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class AreaRoomPlacementSpanPersistenceTests
{
    [Fact]
    public void SpansRoundTripThroughAuthoringAndCleanRuntime_AndLegacyValuesDefaultToOne()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var wideRoom = new Room { Name = "Wide" };
            var tallRoom = new Room { Name = "Tall" };
            var area = new Area
            {
                Name = "Area",
                Rooms = [wideRoom, tallRoom],
                RoomPlacements =
                [
                    new AreaRoomPlacement { RoomId = wideRoom.Id, X = 24, Y = 24, ColumnSpan = 3, RowSpan = 2 },
                    new AreaRoomPlacement { RoomId = tallRoom.Id, X = 24 + 4 * 180, Y = 24,
                        ColumnSpan = 1, RowSpan = 3, FloorElevation = 1 }
                ]
            };
            var project = new ProjectModel
            {
                Name = "Spans",
                Planets = [new Planet { Name = "Planet", Countries =
                    [new Country { Name = "Country", Areas = [area] }] }]
            };
            var projectPath = Path.Combine(tempRoot, "Spans.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, project);

            var authoringPath = Directory.GetFiles(Path.Combine(tempRoot, "Area"), "*.area.json").Single();
            using (var authoring = JsonDocument.Parse(File.ReadAllText(authoringPath)))
            {
                var placements = authoring.RootElement.GetProperty("roomPlacements").EnumerateArray()
                    .ToDictionary(value => value.GetProperty("roomId").GetGuid());
                Assert.Equal(3, placements[wideRoom.Id].GetProperty("columnSpan").GetInt32());
                Assert.Equal(2, placements[wideRoom.Id].GetProperty("rowSpan").GetInt32());
                Assert.Equal(3, placements[tallRoom.Id].GetProperty("rowSpan").GetInt32());
            }

            var loaded = service.TryLoadProjectModel(projectPath)!;
            var loadedArea = loaded.Planets.Single().Countries.Single().Areas.Single();
            Assert.Equal((3, 2), (loadedArea.RoomPlacements[0].ColumnSpan, loadedArea.RoomPlacements[0].RowSpan));
            Assert.Equal((1, 3), (loadedArea.RoomPlacements[1].ColumnSpan, loadedArea.RoomPlacements[1].RowSpan));

            service.ExportCleanProjectV1(projectPath, loaded);
            var runtimePath = Path.Combine(tempRoot, "GameRuntimeJson", "Area",
                area.Id.ToString("D").ToUpperInvariant() + ".runtime.json");
            using (var runtime = JsonDocument.Parse(File.ReadAllText(runtimePath)))
            {
                var placements = runtime.RootElement.GetProperty("roomPlacements").EnumerateArray()
                    .ToDictionary(value => value.GetProperty("roomId").GetGuid());
                Assert.Equal(3, placements[wideRoom.Id].GetProperty("columnSpan").GetInt32());
                Assert.Equal(2, placements[wideRoom.Id].GetProperty("rowSpan").GetInt32());
                Assert.Equal(3, placements[tallRoom.Id].GetProperty("rowSpan").GetInt32());
            }

            var authoringNode = JsonNode.Parse(File.ReadAllText(authoringPath))!;
            var placementNodes = authoringNode["roomPlacements"]!.AsArray();
            placementNodes[0]!.AsObject().Remove("columnSpan");
            placementNodes[0]!.AsObject().Remove("rowSpan");
            placementNodes[1]!["columnSpan"] = null;
            placementNodes[1]!["rowSpan"] = null;
            File.WriteAllText(authoringPath, authoringNode.ToJsonString());

            var legacyLoaded = service.TryLoadProjectModel(projectPath)!;
            Assert.All(legacyLoaded.Planets.Single().Countries.Single().Areas.Single().RoomPlacements,
                placement => Assert.Equal((1, 1), (placement.ColumnSpan, placement.RowSpan)));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }
}
