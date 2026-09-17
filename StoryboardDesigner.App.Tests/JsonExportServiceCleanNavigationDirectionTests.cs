using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceCleanNavigationDirectionTests
{
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
