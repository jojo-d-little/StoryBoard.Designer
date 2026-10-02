using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceCleanNavigationDirectionTests
{
    [Fact]
    public void CleanExport_ThreeSouthPairs_PreserveFootprintDirectionsAndSelectedDoorIds()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var large = new Room { Name = "Large" };
            var neighbors = Enumerable.Range(1, 3).Select(index => new Room { Name = $"South {index}" }).ToArray();
            var sourceDoors = Enumerable.Range(1, 3).Select(index => new GameObject
                { Name = $"South Door {index}", IsOpenable = true }).ToArray();
            var destinationDoors = Enumerable.Range(1, 3).Select(index => new GameObject
                { Name = $"North Door {index}", IsOpenable = true }).ToArray();
            foreach (var door in sourceDoors) large.GameObjects.Add(door);
            for (var index = 0; index < neighbors.Length; index++) neighbors[index].GameObjects.Add(destinationDoors[index]);

            var area = new Area
            {
                Name = "Area",
                Rooms = [large, ..neighbors],
                RoomPlacements =
                [
                    new AreaRoomPlacement { RoomId = large.Id, X = 24, Y = 24, ColumnSpan = 3, RowSpan = 3 },
                    ..neighbors.Select((room, index) => new AreaRoomPlacement
                        { RoomId = room.Id, X = 24 + index * 180, Y = 444 })
                ],
                TraversalConnections = neighbors.Select((room, index) => new TraversalConnection
                {
                    RoomAId = large.Id,
                    RoomBId = room.Id,
                    BaseTraversalDirectionFromA = Direction10.South,
                    TraversalAccessMode = TraversalAccessMode.TwoWay,
                    TraversalStateFromA = new TraversalLegState { OpenableObjectId = sourceDoors[index].ObjectId },
                    TraversalStateFromB = new TraversalLegState { OpenableObjectId = destinationDoors[index].ObjectId }
                }).ToList()
            };
            var project = new ProjectModel
            {
                Name = "Three South Pairs",
                Planets = [new Planet { Name = "Planet", Countries =
                    [new Country { Name = "Country", Areas = [area] }] }]
            };
            var editor = MainWindowViewModelTestHarness.CreateViewModel(
                project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
            editor.OpenAreaEditor(area);
            for (var index = 0; index < neighbors.Length; index++)
            {
                Assert.True(editor.TryLinkTraversalDoors(area.TraversalConnections[index],
                    sourceDoors[index].ObjectId, destinationDoors[index].ObjectId, out var warning), warning);
            }
            var projectFilePath = Path.Combine(tempRoot, "ThreeSouth.sbe.json");
            new JsonExportService().ExportCleanProjectV1(projectFilePath, project);

            using var areaDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindAreaPath(projectFilePath, area.Id), Encoding.UTF8));
            var placements = areaDoc.RootElement.GetProperty("roomPlacements").EnumerateArray()
                .ToDictionary(placement => placement.GetProperty("roomId").GetGuid());
            Assert.Equal(3, placements[large.Id].GetProperty("columnSpan").GetInt32());
            Assert.Equal(3, placements[large.Id].GetProperty("rowSpan").GetInt32());

            var links = areaDoc.RootElement.GetProperty("links").EnumerateArray().ToArray();
            Assert.Equal(6, links.Length);
            for (var index = 0; index < neighbors.Length; index++)
            {
                Assert.Contains(links, link => link.GetProperty("fromRoomId").GetGuid() == large.Id
                    && link.GetProperty("toRoomId").GetGuid() == neighbors[index].Id
                    && link.GetProperty("direction").GetString() == "South"
                    && link.GetProperty("openableObjectId").GetGuid() == sourceDoors[index].ObjectId);
                Assert.Contains(links, link => link.GetProperty("fromRoomId").GetGuid() == neighbors[index].Id
                    && link.GetProperty("toRoomId").GetGuid() == large.Id
                    && link.GetProperty("direction").GetString() == "North"
                    && link.GetProperty("openableObjectId").GetGuid() == destinationDoors[index].ObjectId);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SaveProjectModel_CleanNavigation_PreservesTraversalDirections()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room { Name = "Atrium" };
            var roomB = new Room { Name = "Supply Closet" };

            var area = new Area
            {
                Name = "Area",
                Rooms = new List<Room> { roomA, roomB },
                TraversalConnections = new List<TraversalConnection>
                {
                    new()
                    {
                        RoomAId = roomA.Id,
                        RoomBId = roomB.Id,
                        BaseTraversalDirectionFromA = Direction10.East,
                        TraversalAccessMode = TraversalAccessMode.TwoWay
                    }
                }
            };

            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "DirectionPreservation",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "DirectionPreservation.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var cleanAreaPath = BuildScopeKindAreaPath(projectFilePath, area.Id);
            Assert.True(File.Exists(cleanAreaPath));

            using var areaDoc = JsonDocument.Parse(File.ReadAllText(cleanAreaPath, Encoding.UTF8));
            var links = areaDoc.RootElement
                .GetProperty("links")
                .EnumerateArray()
                .Select(link => new
                {
                    FromRoomId = link.GetProperty("fromRoomId").GetGuid(),
                    ToRoomId = link.GetProperty("toRoomId").GetGuid(),
                    Direction = link.GetProperty("direction").GetString() ?? string.Empty
                })
                .ToList();

            Assert.Contains(links, link =>
                link.FromRoomId == roomA.Id
                && link.ToRoomId == roomB.Id
                && string.Equals(link.Direction, "East", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(links, link =>
                link.FromRoomId == roomB.Id
                && link.ToRoomId == roomA.Id
                && string.Equals(link.Direction, "West", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildScopeKindAreaPath(string projectFilePath, Guid areaId)
    {
        var folder = Path.Combine(Path.GetDirectoryName(projectFilePath) ?? string.Empty, "GameRuntimeJson", "Area");
        return Path.Combine(folder, areaId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }
}
