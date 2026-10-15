using System.Text.Json;
using System.Text.Json.Nodes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomSpawnPointTraversalTests
{
    [Theory]
    [InlineData(Direction10.North, 400, 564)]
    [InlineData(Direction10.South, 400, 36)]
    [InlineData(Direction10.East, 36, 300)]
    [InlineData(Direction10.West, 764, 300)]
    [InlineData(Direction10.NorthEast, 36, 564)]
    [InlineData(Direction10.SouthWest, 764, 36)]
    [InlineData(Direction10.Up, 400, 300)]
    [InlineData(Direction10.Down, 400, 300)]
    public void EnsureAreaSpawnPoints_CreatesEntryAtExpectedWallOrCenter(Direction10 direction, double expectedX, double expectedY)
    {
        var source = new Room { Name = "Source" };
        var destination = new Room { Name = "Destination" };
        var connection = new TraversalConnection
        {
            RoomAId = source.Id,
            RoomBId = destination.Id,
            BaseTraversalDirectionFromA = direction,
            TraversalAccessMode = TraversalAccessMode.OneWayAtoB
        };
        var area = new Area { Rooms = [source, destination], TraversalConnections = [connection] };

        RoomSpawnPointAuthoringService.EnsureAreaSpawnPoints(area);

        var entry = Assert.Single(destination.SpawnPoints, point => point.TraversalConnectionId == connection.TraversalConnectionId);
        Assert.Equal((expectedX, expectedY), (entry.X, entry.Y));
        Assert.Equal(destination.GameObjects.Count == 0 ? 1001 : destination.GameObjects.Max(obj => obj.RenderZOrder) + 1, entry.RenderZOrder);
        Assert.DoesNotContain(destination.SpawnPoints, point => point.IsDefault);
        Assert.DoesNotContain(source.SpawnPoints, point => point.TraversalConnectionId == connection.TraversalConnectionId);
    }

    [Fact]
    public void EnsureAreaSpawnPoints_PreservesAuthoredPositionsAndAllowsDefaultToAlsoReferenceTraversal()
    {
        var source = new Room { Name = "Source" };
        var destination = new Room { Name = "Destination", SpawnPoints =
        [
            new RoomSpawnPoint(171, 223, 9, IsDefault: true, TraversalConnectionId: Guid.NewGuid(), FacingOverride: Direction10.East)
        ] };
        var traversalId = destination.SpawnPoints[0].TraversalConnectionId!.Value;
        var connection = new TraversalConnection
        {
            TraversalConnectionId = traversalId,
            RoomAId = source.Id,
            RoomBId = destination.Id,
            BaseTraversalDirectionFromA = Direction10.South,
            TraversalAccessMode = TraversalAccessMode.OneWayAtoB
        };
        var area = new Area { Rooms = [source, destination], TraversalConnections = [connection] };

        RoomSpawnPointAuthoringService.EnsureAreaSpawnPoints(area);
        RoomSpawnPointAuthoringService.EnsureAreaSpawnPoints(area);

        Assert.Equal(new RoomSpawnPoint(171, 223, 9, true, traversalId, Direction10.East), Assert.Single(destination.SpawnPoints));
        Assert.Empty(source.SpawnPoints);
        Assert.Null(source.SpawnPoint.TraversalConnectionId);
    }

    [Fact]
    public void EnsureAreaSpawnPoints_CreatesDistinctPointsForSameDirectionAndDestinationRooms()
    {
        var sourceA = new Room { Name = "North Hall" };
        var sourceB = new Room { Name = "North Gallery" };
        var destination = new Room { Name = "Atrium" };
        var first = new TraversalConnection { RoomAId = sourceA.Id, RoomBId = destination.Id, BaseTraversalDirectionFromA = Direction10.South, TraversalAccessMode = TraversalAccessMode.OneWayAtoB };
        var second = new TraversalConnection { RoomAId = sourceB.Id, RoomBId = destination.Id, BaseTraversalDirectionFromA = Direction10.South, TraversalAccessMode = TraversalAccessMode.OneWayAtoB };
        var area = new Area { Rooms = [sourceA, sourceB, destination], TraversalConnections = [first, second] };

        RoomSpawnPointAuthoringService.EnsureAreaSpawnPoints(area);

        var points = destination.SpawnPoints.Where(point => point.TraversalConnectionId.HasValue).ToList();
        Assert.Equal(2, points.Count);
        Assert.Equal(2, points.Select(point => point.TraversalConnectionId).Distinct().Count());
        Assert.NotEqual(first.TraversalConnectionId, second.TraversalConnectionId);
        var tab = new StoryboardDesigner.App.ViewModels.RoomDesignerTabViewModel(destination, 800, 600, area: area);
        Assert.Contains("North Hall", tab.RoomRenderOrderItems.Single(item => item.SpawnPoint?.TraversalConnectionId == first.TraversalConnectionId).ToolTipText);
        Assert.Contains("North Gallery", tab.RoomRenderOrderItems.Single(item => item.SpawnPoint?.TraversalConnectionId == second.TraversalConnectionId).ToolTipText);
    }

    [Fact]
    public void OneWayTraversal_CreatesOnlyPermittedIncomingPoint()
    {
        var roomA = new Room();
        var roomB = new Room();
        var aToB = new TraversalConnection { RoomAId = roomA.Id, RoomBId = roomB.Id, TraversalAccessMode = TraversalAccessMode.OneWayAtoB };
        var area = new Area { Rooms = [roomA, roomB], TraversalConnections = [aToB] };

        RoomSpawnPointAuthoringService.EnsureAreaSpawnPoints(area);

        Assert.Contains(roomB.SpawnPoints, point => point.TraversalConnectionId == aToB.TraversalConnectionId);
        Assert.DoesNotContain(roomA.SpawnPoints, point => point.TraversalConnectionId == aToB.TraversalConnectionId);
        var tab = new StoryboardDesigner.App.ViewModels.RoomDesignerTabViewModel(roomB, 800, 600, area: area);
        var marker = Assert.Single(tab.RoomRenderOrderItems, item => item.SpawnPoint?.TraversalConnectionId == aToB.TraversalConnectionId);
        Assert.Equal("S", marker.SpawnPointMarkerText);
    }

    [Fact]
    public void GeneratedPositionsUpdateAfterReloadWhenGeometryChangesButProducerMovesArePreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = new Room { Name = "Source" };
            var destination = new Room { Name = "Destination" };
            var connection = new TraversalConnection
            {
                RoomAId = source.Id,
                RoomBId = destination.Id,
                BaseTraversalDirectionFromA = Direction10.East,
                TraversalAccessMode = TraversalAccessMode.OneWayAtoB
            };
            var area = new Area { Name = "Area", Rooms = [source, destination], TraversalConnections = [connection] };
            var project = new ProjectModel { Name = "Generated positions", Planets = [new Planet { Countries = [new Country { Areas = [area] }] }] };
            var path = Path.Combine(root, "Generated.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(path, project);

            var reloaded = service.TryLoadProjectModel(path)!;
            var loadedArea = reloaded.Planets.Single().Countries.Single().Areas.Single();
            var loadedDestination = loadedArea.Rooms.Single(room => room.Id == destination.Id);
            loadedDestination.RoomImageCanvasWidth = 1200;
            loadedArea.TraversalConnections.Single().BaseTraversalDirectionFromA = Direction10.West;
            RoomSpawnPointAuthoringService.EnsureAreaSpawnPoints(loadedArea);
            var regenerated = Assert.Single(loadedDestination.SpawnPoints, point => point.TraversalConnectionId == connection.TraversalConnectionId);
            Assert.Equal(1164, regenerated.X);
            Assert.True(regenerated.IsAutoGeneratedPosition);

            var moved = regenerated with { X = 711, IsAutoGeneratedPosition = false, GeneratedPositionX = null, GeneratedPositionY = null };
            loadedDestination.SpawnPoints = loadedDestination.SpawnPoints.Select(point => point.TraversalConnectionId == connection.TraversalConnectionId ? moved : point).ToList();
            service.SaveProjectModel(path, reloaded);
            var editedReload = service.TryLoadProjectModel(path)!;
            var editedArea = editedReload.Planets.Single().Countries.Single().Areas.Single();
            var editedDestination = editedArea.Rooms.Single(room => room.Id == destination.Id);
            editedDestination.RoomImageCanvasWidth = 1600;
            RoomSpawnPointAuthoringService.EnsureAreaSpawnPoints(editedArea);
            var preserved = Assert.Single(editedDestination.SpawnPoints, point => point.TraversalConnectionId == connection.TraversalConnectionId);
            Assert.Equal(711, preserved.X);
            Assert.False(preserved.IsAutoGeneratedPosition);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ValidationReportsUnknownWrongRoomDuplicateMissingAndInvalidDefaultPoints()
    {
        var (project, area, roomA, roomB, roomC, connection) = MakeArea();
        roomA.SpawnPoints =
        [
            new RoomSpawnPoint(1, 1, IsDefault: false, TraversalConnectionId: Guid.NewGuid()),
            new RoomSpawnPoint(2, 2, IsDefault: false, TraversalConnectionId: connection.TraversalConnectionId),
            new RoomSpawnPoint(3, 3, IsDefault: false, TraversalConnectionId: connection.TraversalConnectionId)
        ];
        roomB.SpawnPoints = [new RoomSpawnPoint(0, 0, IsDefault: true)];
        roomC.SpawnPoints =
        [
            new RoomSpawnPoint(0, 0, IsDefault: true),
            new RoomSpawnPoint(1, 1, IsDefault: true),
            new RoomSpawnPoint(4, 4, IsDefault: false, TraversalConnectionId: connection.TraversalConnectionId)
        ];

        var issues = Validate(project, area);

        Assert.Contains(issues, issue => issue.Description.Contains("unknown traversal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Description.Contains("not connected", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Description.Contains("duplicate spawn-point", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Description.Contains("more than one default", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Description.Contains("missing a spawn point", StringComparison.OrdinalIgnoreCase));
        Assert.All(issues, issue => Assert.Contains("Room/", issue.Path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(TraversalAccessMode.OneWayAtoB, true, false)]
    [InlineData(TraversalAccessMode.OneWayBtoA, false, true)]
    [InlineData(TraversalAccessMode.TwoWay, true, true)]
    public void ValidationUsesTraversalAccessDirectionForRequiredArrivals(TraversalAccessMode access, bool roomBReceives, bool roomAReceives)
    {
        var (project, area, roomA, roomB, _, connection) = MakeArea(access);
        var issues = Validate(project, area);
        var missingPaths = issues.Where(issue => issue.Description.Contains("missing a spawn point", StringComparison.OrdinalIgnoreCase))
            .Select(issue => issue.Path).ToList();

        Assert.Equal(roomBReceives, missingPaths.Any(path => path.Contains($"Room/{roomB.Name}", StringComparison.Ordinal)));
        Assert.Equal(roomAReceives, missingPaths.Any(path => path.Contains($"Room/{roomA.Name}", StringComparison.Ordinal)));
        Assert.DoesNotContain(issues, issue => issue.Description.Contains(connection.TraversalConnectionId.ToString("D"), StringComparison.OrdinalIgnoreCase)
            && issue.Description.Contains("unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SpawnPointsAndTraversalIdentity_RoundTripThroughProjectAndRuntimeExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var roomA = new Room { Name = "A", SpawnPoints = [new RoomSpawnPoint(12, 34, 3, true, null, Direction10.East)] };
            var roomB = new Room { Name = "B", SpawnPoints = [new RoomSpawnPoint(200, 210, 8, true, null, Direction10.West)] };
            var connection = new TraversalConnection { RoomAId = roomA.Id, RoomBId = roomB.Id, BaseTraversalDirectionFromA = Direction10.East };
            roomA.SpawnPoints = roomA.SpawnPoints.Append(new RoomSpawnPoint(71, 81, 9, false, connection.TraversalConnectionId, Direction10.South)).ToList();
            roomB.SpawnPoints = roomB.SpawnPoints.Append(new RoomSpawnPoint(91, 101, 10, false, connection.TraversalConnectionId, Direction10.North)).ToList();
            var area = new Area { Name = "Area", Rooms = [roomA, roomB], TraversalConnections = [connection], RoomPlacements = [new() { RoomId = roomA.Id }, new() { RoomId = roomB.Id, X = 180 }] };
            var template = new Room { Name = "Template", DesignerPersistenceScopeKind = ScopeNodeKind.Templates,
                SpawnPoints = [new RoomSpawnPoint(10, 20, IsDefault: true, TraversalConnectionId: connection.TraversalConnectionId),
                    new RoomSpawnPoint(30, 40, IsDefault: false, TraversalConnectionId: connection.TraversalConnectionId)] };
            var project = new ProjectModel { Name = "Spawn identity", RoomTemplates = [template], Planets = [new Planet { Countries = [new Country { Areas = [area] }] }] };
            var path = Path.Combine(root, "SpawnIdentity.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(path, project);
            var reloaded = service.TryLoadProjectModel(path)!;
            var reloadedArea = reloaded.Planets.Single().Countries.Single().Areas.Single();
            Assert.Equal(connection.TraversalConnectionId, reloadedArea.TraversalConnections.Single().TraversalConnectionId);
            Assert.Contains(reloadedArea.Rooms.Single(room => room.Id == roomB.Id).SpawnPoints,
                point => point.TraversalConnectionId == connection.TraversalConnectionId && point.X == 91 && point.RenderZOrder == 10 && point.FacingOverride == Direction10.North);
            var loadedTemplate = reloaded.RoomTemplates.Single(room => room.Id == template.Id);
            Assert.Single(loadedTemplate.SpawnPoints);
            Assert.True(loadedTemplate.SpawnPoint.IsDefault);
            Assert.Null(loadedTemplate.SpawnPoint.TraversalConnectionId);

            service.ExportCleanProjectV1(path, reloaded);
            var runtimeRoomPath = Path.Combine(root, "GameRuntimeJson", "Room", roomB.Id.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtimeRoom = JsonDocument.Parse(File.ReadAllText(runtimeRoomPath));
            Assert.Contains(runtimeRoom.RootElement.GetProperty("spawnPoints").EnumerateArray(), point =>
                point.TryGetProperty("traversalConnectionId", out var traversalId)
                && traversalId.GetGuid() == connection.TraversalConnectionId
                && point.GetProperty("x").GetDouble() == 91
                && point.GetProperty("renderZOrder").GetInt32() == 10
                && point.GetProperty("facingOverride").GetString() == "North");
            var runtimeAreaPath = Path.Combine(root, "GameRuntimeJson", "Area", area.Id.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtimeArea = JsonDocument.Parse(File.ReadAllText(runtimeAreaPath));
            var links = runtimeArea.RootElement.GetProperty("links").EnumerateArray().ToList();
            Assert.Equal(2, links.Count);
            Assert.All(links, link => Assert.Equal(connection.TraversalConnectionId, link.GetProperty("traversalConnectionId").GetGuid()));
            Assert.Contains(links, link => link.GetProperty("fromRoomId").GetGuid() == roomA.Id && link.GetProperty("toRoomId").GetGuid() == roomB.Id);
            Assert.Contains(links, link => link.GetProperty("fromRoomId").GetGuid() == roomB.Id && link.GetProperty("toRoomId").GetGuid() == roomA.Id);

            var authoringRoomPath = Path.Combine(root, "Room", roomB.Id.ToString("N") + ".room.json");
            var legacyRoom = JsonNode.Parse(File.ReadAllText(authoringRoomPath))!.AsObject();
            legacyRoom.Remove("spawnPoints");
            legacyRoom["spawnPoint"] = new JsonObject { ["x"] = 17, ["y"] = 19, ["renderZOrder"] = 5 };
            File.WriteAllText(authoringRoomPath, legacyRoom.ToJsonString());
            var legacyLoaded = service.TryLoadProjectModel(path)!;
            var migratedDefault = legacyLoaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single(room => room.Id == roomB.Id).SpawnPoint;
            Assert.Equal((17, 19, 5, true), (migratedDefault.X, migratedDefault.Y, migratedDefault.RenderZOrder, migratedDefault.IsDefault));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static IReadOnlyList<ValidationIssue> Validate(ProjectModel project, Area area)
    {
        var context = ValidationRuleTestContext.Create(project) with { CandidateNode = area };
        return new RoomSpawnPointTraversalIntegrityRule().Evaluate(context).ToList();
    }

    private static (ProjectModel Project, Area Area, Room RoomA, Room RoomB, Room RoomC, TraversalConnection Connection) MakeArea(
        TraversalAccessMode access = TraversalAccessMode.TwoWay)
    {
        var roomA = new Room { Name = "A", SpawnPoints = [new RoomSpawnPoint(0, 0, IsDefault: true)] };
        var roomB = new Room { Name = "B", SpawnPoints = [new RoomSpawnPoint(0, 0, IsDefault: true)] };
        var roomC = new Room { Name = "C", SpawnPoints = [new RoomSpawnPoint(0, 0, IsDefault: true)] };
        var connection = new TraversalConnection { RoomAId = roomA.Id, RoomBId = roomB.Id, TraversalAccessMode = access };
        var area = new Area { Name = "Area", Rooms = [roomA, roomB, roomC], TraversalConnections = [connection] };
        var project = new ProjectModel { Planets = [new Planet { Countries = [new Country { Areas = [area] }] }] };
        return (project, area, roomA, roomB, roomC, connection);
    }
}
