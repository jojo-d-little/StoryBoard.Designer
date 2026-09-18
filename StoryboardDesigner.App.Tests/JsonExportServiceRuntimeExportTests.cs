using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public class JsonExportServiceRuntimeExportTests
{
    private const string RuntimeExportFolderName = "GameRuntimeJson";
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2F3iUAAAAASUVORK5CYII=";

    [Fact]
    public void ExportRuntimeProjectV1_WritesProjectAndScopeKindFiles_WithTopLevelSchemaVersion()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "CleanContract.sbe.json");
            var room = new Room { Name = "Room One" };
            var area = new Area
            {
                Name = "Area One",
                Rooms = new List<Room> { room },
                Links = new List<RoomLink>(),
                RoomPlacements = new List<AreaRoomPlacement>
                {
                    new() { RoomId = room.Id, X = 100, Y = 200 }
                }
            };
            var country = new Country { Name = "Country One", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet One", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "CleanContract",
                AutoSaveSeconds = 30,
                Planets = new List<Planet> { planet }
            };

            var cleanProjectPath = service.ExportCleanProjectV1(projectFilePath, project);

            Assert.True(File.Exists(cleanProjectPath));
            Assert.EndsWith(".sbr.runtime.json", cleanProjectPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"{Path.DirectorySeparatorChar}{RuntimeExportFolderName}{Path.DirectorySeparatorChar}", cleanProjectPath, StringComparison.OrdinalIgnoreCase);

            var legacyAggregateNavigationPath = BuildLegacyRuntimeNavigationPath(projectFilePath);
            Assert.False(File.Exists(legacyAggregateNavigationPath));

            var cleanRoomPath = BuildRuntimeRoomPath(projectFilePath, room.Id);
            Assert.True(File.Exists(cleanRoomPath));

            var scopeKindPlanetPath = BuildScopeKindPlanetNodePath(projectFilePath, planet.Id);
            Assert.True(File.Exists(scopeKindPlanetPath));

            var scopeKindCountryPath = BuildScopeKindCountryNodePath(projectFilePath, country.Id);
            Assert.True(File.Exists(scopeKindCountryPath));

            var scopeKindAreaPath = BuildScopeKindAreaNodePath(projectFilePath, area.Id);
            Assert.True(File.Exists(scopeKindAreaPath));

            var scopeKindRoomPath = BuildScopeKindRoomNodePath(projectFilePath, room.Id);
            Assert.True(File.Exists(scopeKindRoomPath));

            using var scopeKindRoomDoc = JsonDocument.Parse(File.ReadAllText(scopeKindRoomPath, Encoding.UTF8));
            Assert.False(scopeKindRoomDoc.RootElement.TryGetProperty("gameObjects", out _));
            Assert.False(scopeKindRoomDoc.RootElement.TryGetProperty("gameObjectIds", out _));

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(cleanProjectPath, Encoding.UTF8));
            Assert.Equal("1.0", projectDoc.RootElement.GetProperty("schemaVersion").GetString());
            Assert.False(projectDoc.RootElement.TryGetProperty("uiState", out _));

            using var scopeKindAreaDoc = JsonDocument.Parse(File.ReadAllText(scopeKindAreaPath, Encoding.UTF8));
            Assert.True(scopeKindAreaDoc.RootElement.TryGetProperty("links", out _));
            Assert.True(scopeKindAreaDoc.RootElement.TryGetProperty("roomPlacements", out _));

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(cleanRoomPath, Encoding.UTF8));
            Assert.False(roomDoc.RootElement.TryGetProperty("schemaVersion", out _));
            Assert.Equal("Room One", roomDoc.RootElement.GetProperty("name").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_WritesPhaseScopeKindFiles_AndProjectPhasePointers()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var page = new PhaseNode
            {
                Tier = PhaseTier.Page,
                PhaseKey = "book.one.chapter.one.page.one",
                DisplayName = "Page One",
                Narrative = "Page One Narrative"
            };

            var chapter = new PhaseNode
            {
                Tier = PhaseTier.Chapter,
                PhaseKey = "book.one.chapter.one",
                DisplayName = "Chapter One"
            };
            chapter.AddChildScope(page);

            var book = new PhaseNode
            {
                Tier = PhaseTier.Book,
                PhaseKey = "book.one",
                DisplayName = "Book One"
            };
            book.AddChildScope(chapter);

            var project = new ProjectModel
            {
                Name = "CleanPhaseContract",
                PhaseBooks = new List<PhaseNode> { book },
                StartingPhasePageId = page.Id
            };

            var projectFilePath = Path.Combine(tempRoot, "CleanPhaseContract.sbe.json");
            var cleanProjectPath = service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(cleanProjectPath, Encoding.UTF8));
            Assert.True(projectDoc.RootElement.TryGetProperty("phaseBookIds", out var phaseBookIdsElement));
            Assert.Single(phaseBookIdsElement.EnumerateArray());
            Assert.Equal(book.Id, phaseBookIdsElement.EnumerateArray().Single().GetGuid());
            Assert.Equal(page.Id, projectDoc.RootElement.GetProperty("startingPhasePageId").GetGuid());

            var bookPath = BuildScopeKindBookNodePath(projectFilePath, book.Id);
            var chapterPath = BuildScopeKindBookNodePath(projectFilePath, chapter.Id);
            var pagePath = BuildScopeKindBookNodePath(projectFilePath, page.Id);
            Assert.True(File.Exists(bookPath));
            Assert.True(File.Exists(chapterPath));
            Assert.True(File.Exists(pagePath));

            using var bookDoc = JsonDocument.Parse(File.ReadAllText(bookPath, Encoding.UTF8));
            Assert.Equal("Book", bookDoc.RootElement.GetProperty("scopeKind").GetString());
            Assert.Equal(book.PhaseKey, bookDoc.RootElement.GetProperty("phaseKey").GetString());
            var bookChildIds = bookDoc.RootElement.GetProperty("childPhaseIds").EnumerateArray().Select(static value => value.GetGuid()).ToList();
            Assert.Contains(chapter.Id, bookChildIds);

            using var chapterDoc = JsonDocument.Parse(File.ReadAllText(chapterPath, Encoding.UTF8));
            Assert.Equal("Chapter", chapterDoc.RootElement.GetProperty("scopeKind").GetString());
            Assert.Equal(book.Id, chapterDoc.RootElement.GetProperty("parentPhaseId").GetGuid());

            using var pageDoc = JsonDocument.Parse(File.ReadAllText(pagePath, Encoding.UTF8));
            Assert.Equal("Page", pageDoc.RootElement.GetProperty("scopeKind").GetString());
            Assert.Equal(chapter.Id, pageDoc.RootElement.GetProperty("parentPhaseId").GetGuid());
            Assert.Equal("Page One Narrative", pageDoc.RootElement.GetProperty("narrative").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_EmbedsAreaNavigationPayload_InScopeKindAreaFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room { Name = "Room A" };
            var roomB = new Room { Name = "Room B" };

            var connection = new TraversalConnection
            {
                TraversalConnectionId = Guid.NewGuid(),
                RoomAId = roomA.Id,
                RoomBId = roomB.Id,
                BaseTraversalDirectionFromA = Direction10.East,
                TraversalAccessMode = TraversalAccessMode.TwoWay,
                TraversalStateFromA = new TraversalLegState
                {
                    OpenStatePolicy = OpenablePolicy.RequireOpen,
                    SharedVariableId = Guid.NewGuid()
                },
                TraversalStateFromB = new TraversalLegState
                {
                    OpenStatePolicy = OpenablePolicy.RequireOpen
                },
                OpenStateBindingMode = OpenStateBindingMode.Together
            };

            var area = new Area
            {
                Name = "Area One",
                Rooms = new List<Room> { roomA, roomB },
                TraversalConnections = new List<TraversalConnection> { connection },
                RoomPlacements = new List<AreaRoomPlacement>
                {
                    new() { RoomId = roomA.Id, X = 120, Y = 210 },
                    new() { RoomId = roomB.Id, X = 320, Y = 210 }
                }
            };

            var country = new Country { Name = "Country One", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet One", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "AreaNavigationEmbedding",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "AreaNavigationEmbedding.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var scopeKindAreaPath = BuildScopeKindAreaNodePath(projectFilePath, area.Id);
            Assert.True(File.Exists(scopeKindAreaPath));

            using var areaDoc = JsonDocument.Parse(File.ReadAllText(scopeKindAreaPath, Encoding.UTF8));
            Assert.True(areaDoc.RootElement.TryGetProperty("links", out var links));
            Assert.True(areaDoc.RootElement.TryGetProperty("roomPlacements", out var roomPlacements));
            Assert.False(areaDoc.RootElement.TryGetProperty("traversalConnections", out _));

            Assert.Equal(2, links.GetArrayLength());
            Assert.Equal(2, roomPlacements.GetArrayLength());

            var exportedDirections = links.EnumerateArray()
                .Select(link => link.GetProperty("direction").GetString())
                .Where(static direction => !string.IsNullOrWhiteSpace(direction))
                .ToList();
            Assert.Contains("East", exportedDirections);
            Assert.Contains("West", exportedDirections);

            Assert.All(links.EnumerateArray(), link =>
            {
                Assert.False(link.TryGetProperty("defaultIsPassable", out _));
                Assert.False(link.TryGetProperty("sharedVariableId", out _));
                Assert.True(link.TryGetProperty("gameProperties", out var gameProperties));
                Assert.True(gameProperties.ValueKind == JsonValueKind.Array);
                Assert.True(link.TryGetProperty("openStateBindingMode", out _));
            });
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_DoesNotWriteAggregateNavigationFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room { Name = "Room A" };
            var roomB = new Room { Name = "Room B" };
            var connection = new TraversalConnection
            {
                TraversalConnectionId = Guid.NewGuid(),
                RoomAId = roomA.Id,
                RoomBId = roomB.Id,
                BaseTraversalDirectionFromA = Direction10.East,
                TraversalAccessMode = TraversalAccessMode.TwoWay,
                TraversalStateFromA = new TraversalLegState(),
                TraversalStateFromB = new TraversalLegState()
            };

            var area = new Area
            {
                Name = "Area One",
                Rooms = new List<Room> { roomA, roomB },
                TraversalConnections = new List<TraversalConnection> { connection },
                RoomPlacements = new List<AreaRoomPlacement>
                {
                    new() { RoomId = roomA.Id, X = 100, Y = 100 },
                    new() { RoomId = roomB.Id, X = 200, Y = 100 }
                }
            };

            var country = new Country { Name = "Country One", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet One", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "NoAggregateNavigation",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "NoAggregateNavigation.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var legacyAggregateNavigationPath = BuildLegacyRuntimeNavigationPath(projectFilePath);
            Assert.False(File.Exists(legacyAggregateNavigationPath));

            var areaScopePath = BuildScopeKindAreaNodePath(projectFilePath, area.Id);
            Assert.True(File.Exists(areaScopePath));

            using var areaDoc = JsonDocument.Parse(File.ReadAllText(areaScopePath, Encoding.UTF8));
            Assert.True(areaDoc.RootElement.TryGetProperty("links", out var links));
            Assert.True(areaDoc.RootElement.TryGetProperty("roomPlacements", out var roomPlacements));
            Assert.False(areaDoc.RootElement.TryGetProperty("traversalConnections", out _));
            Assert.Equal(2, links.GetArrayLength());
            Assert.Equal(2, roomPlacements.GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_WritesRoomPlacementFloorElevation_WhenNonZero()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room { Name = "Room A" };
            var roomB = new Room { Name = "Room B" };

            var area = new Area
            {
                Name = "Area One",
                Rooms = new List<Room> { roomA, roomB },
                RoomPlacements = new List<AreaRoomPlacement>
                {
                    new() { RoomId = roomA.Id, X = 100, Y = 100, FloorElevation = 0 },
                    new() { RoomId = roomB.Id, X = 200, Y = 100, FloorElevation = 2 }
                }
            };

            var country = new Country { Name = "Country One", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet One", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "RoomPlacementFloorElevation",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "RoomPlacementFloorElevation.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var areaScopePath = BuildScopeKindAreaNodePath(projectFilePath, area.Id);
            Assert.True(File.Exists(areaScopePath));

            using var areaDoc = JsonDocument.Parse(File.ReadAllText(areaScopePath, Encoding.UTF8));
            var roomPlacements = areaDoc.RootElement.GetProperty("roomPlacements");
            var placementsByRoomId = roomPlacements.EnumerateArray()
                .ToDictionary(
                    p => p.GetProperty("roomId").GetGuid(),
                    p => p);

            Assert.Equal(2, placementsByRoomId.Count);
            Assert.False(placementsByRoomId[roomA.Id].TryGetProperty("floorElevation", out _));
            Assert.Equal(2, placementsByRoomId[roomB.Id].GetProperty("floorElevation").GetInt32());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_IsDeterministic_ForSameProjectInput()
    {
        var service = new JsonExportService();
        var rootA = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        var rootB = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootA);
        Directory.CreateDirectory(rootB);

        try
        {
            var room = new Room { Name = "Deterministic Room" };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "Deterministic",
                AutoSaveSeconds = 15,
                CommandVerbs = new List<string> { "look", "go" },
                Directionals = new List<string> { "north", "south" },
                Planets = new List<Planet> { planet }
            };

            var pathA = Path.Combine(rootA, "Deterministic.sbe.json");
            var pathB = Path.Combine(rootB, "Deterministic.sbe.json");

            service.ExportCleanProjectV1(pathA, project);
            service.ExportCleanProjectV1(pathB, project);

            Assert.Equal(
                File.ReadAllText(BuildRuntimeProjectPath(pathA), Encoding.UTF8),
                File.ReadAllText(BuildRuntimeProjectPath(pathB), Encoding.UTF8));

            Assert.False(File.Exists(BuildLegacyRuntimeNavigationPath(pathA)));
            Assert.False(File.Exists(BuildLegacyRuntimeNavigationPath(pathB)));

            Assert.Equal(
                File.ReadAllText(BuildRuntimeRoomPath(pathA, room.Id), Encoding.UTF8),
                File.ReadAllText(BuildRuntimeRoomPath(pathB, room.Id), Encoding.UTF8));

            Assert.Equal(
                File.ReadAllText(BuildScopeKindPlanetNodePath(pathA, planet.Id), Encoding.UTF8),
                File.ReadAllText(BuildScopeKindPlanetNodePath(pathB, planet.Id), Encoding.UTF8));

            Assert.Equal(
                File.ReadAllText(BuildScopeKindCountryNodePath(pathA, country.Id), Encoding.UTF8),
                File.ReadAllText(BuildScopeKindCountryNodePath(pathB, country.Id), Encoding.UTF8));

            Assert.Equal(
                File.ReadAllText(BuildScopeKindAreaNodePath(pathA, area.Id), Encoding.UTF8),
                File.ReadAllText(BuildScopeKindAreaNodePath(pathB, area.Id), Encoding.UTF8));

            Assert.Equal(
                File.ReadAllText(BuildScopeKindRoomNodePath(pathA, room.Id), Encoding.UTF8),
                File.ReadAllText(BuildScopeKindRoomNodePath(pathB, room.Id), Encoding.UTF8));
        }
        finally
        {
            if (Directory.Exists(rootA))
            {
                Directory.Delete(rootA, recursive: true);
            }

            if (Directory.Exists(rootB))
            {
                Directory.Delete(rootB, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_IncludesObjectAppearanceFields_WhenConfigured()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceImagesPath = Path.Combine(tempRoot, "source");
            Directory.CreateDirectory(sourceImagesPath);

            var mainPath = WriteTinyPng(sourceImagesPath, "clean-main.png");
            var grayPath = WriteTinyPng(sourceImagesPath, "clean-gray.png");
            var normalPath = WriteTinyPng(sourceImagesPath, "clean-normal.png");

            var gameObject = new GameObject
            {
                Name = "DisplayObject",
                ImageRotationDegrees = 15,
                PositionX = 12,
                PositionY = 24,
                FootprintWidthCells = 2,
                FootprintHeightCells = 1,
                FootprintOrientation = "E",
                AuthoredRenderOrder = 1337,
                AuthoredBaseHeightInRoom = 2,
                IsHeightPinned = true,
                OccupiedCellIds = ["A.A", "B.A"],
                OccupancyDerivationSourceEcho = "x=12;y=24;anchor=TopLeft;footprint=2x1;orientation=E;cell=40",
                ImageVariantChooserScript = "if ({{object.isOpen}} == true) { return \"default\"; }",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = mainPath,
                        ImageScale = 1.5,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room { Name = "Visual Room", GameObjects = new List<GameObject> { gameObject } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "CleanObjectAppearance", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "CleanObjectAppearance.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var exportedObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var exportedVariant = exportedObject.GetProperty("imageVariants").EnumerateArray().Single();
            var fullImagePath = exportedVariant.GetProperty("fullImagePath").GetString();

            var gameObjectId = exportedObject.GetProperty("Id").GetGuid();
            var scopeKindGameObjectPath = BuildScopeKindGameObjectNodePath(projectFilePath, gameObjectId);
            Assert.True(File.Exists(scopeKindGameObjectPath));

            using var scopeKindRoomDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindRoomNodePath(projectFilePath, room.Id), Encoding.UTF8));
            Assert.False(scopeKindRoomDoc.RootElement.TryGetProperty("gameObjects", out _));
            var scopeKindGameObjectIds = scopeKindRoomDoc.RootElement.GetProperty("gameObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            Assert.Contains(gameObjectId, scopeKindGameObjectIds);

            Assert.Equal("default", exportedVariant.GetProperty("variantName").GetString());
            Assert.Equal("runtimeExportRelative", exportedVariant.GetProperty("imagePathSemantics").GetString());
            Assert.StartsWith("assets/images/_shared/", fullImagePath, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(BuildRuntimeExportRootPath(projectFilePath), fullImagePath!.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(15, exportedObject.GetProperty("imageRotationDegrees").GetDouble(), 3);
            Assert.Equal(1337, exportedObject.GetProperty("authoredRenderOrder").GetInt32());
            Assert.Equal("if ({{object.isOpen}} == true) { return \"default\"; }", exportedObject.GetProperty("imageVariantChooserScript").GetString());
            Assert.Equal(1.5, exportedVariant.GetProperty("imageScale").GetDouble(), 3);

            var appearance = exportedObject.GetProperty("appearance");
            Assert.Equal(2, appearance.GetProperty("authoredBaseHeightInRoom").GetInt32());
            Assert.True(appearance.GetProperty("isHeightPinned").GetBoolean());
            var occupiedCells = appearance.GetProperty("occupiedCellIds").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Equal(new[] { "A.A", "B.A" }, occupiedCells);
            Assert.Equal("x=12;y=24;anchor=TopLeft;footprint=2x1;orientation=E;cell=40", appearance.GetProperty("occupancyDerivationSourceEcho").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_IncludesMovementRestrictionDirectionalRules_InCleanRoomAppearance()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var gameObject = new GameObject
            {
                Name = "FlaggedMover",
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions
                {
                    MultiLegMaxTotalDistanceCells = 5,
                    SubsequentStacked = new ObjectMovementRestrictionCategory
                    {
                        NE = new ObjectMovementRestrictionRule
                        {
                            MaxDistance = 3,
                            AllowJumpOver = true
                        }
                    }
                }
            };

            var room = new Room { Name = "Movement Room", GameObjects = new List<GameObject> { gameObject } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "CleanMovementRestrictionFlags", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "CleanMovementRestrictionFlags.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var exportedObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var appearance = exportedObject.GetProperty("appearance");
            var movementRestrictions = appearance.GetProperty("movementRestrictions");
            Assert.Equal(5, movementRestrictions.GetProperty("multiLegMaxTotalDistanceCells").GetInt32());
            var subsequentStacked = movementRestrictions.GetProperty("subsequentStacked");
            var ne = subsequentStacked.GetProperty("ne");

            Assert.Equal(3, ne.GetProperty("maxDistance").GetInt32());
            Assert.True(ne.GetProperty("allowJumpOver").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_IncludesObjectNameSynonyms_InCleanRoomJson()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var gameObject = new GameObject
            {
                Name = "Knife",
                NameInGame = "Kitchen Knife",
                NameSynonyms = ["blade", "shiv", "blade"]
            };

            var room = new Room { Name = "Room", GameObjects = new List<GameObject> { gameObject } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "CleanNameSynonyms", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "CleanNameSynonyms.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var exportedObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var savedSynonyms = exportedObject.GetProperty("nameSynonyms")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            Assert.Equal(2, savedSynonyms.Count);
            Assert.Contains("blade", savedSynonyms, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("shiv", savedSynonyms, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_IncludesScopedAuthoredGameObjects_ForPlanetCountryArea()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room { Name = "Room" };
            var areaBaseObject = new GameObject { Name = "Area Base Relic" };
            var areaGameObject = new GameObject { Name = "Area Relic" };
            var area = new Area
            {
                Name = "Area",
                Rooms = new List<Room> { room },
                BaseObjects = new List<GameObject> { areaBaseObject },
                GameObjects = new List<GameObject> { areaGameObject }
            };

            var countryBaseObject = new GameObject { Name = "Country Base Relic" };
            var countryGameObject = new GameObject { Name = "Country Relic" };
            var country = new Country
            {
                Name = "Country",
                Areas = new List<Area> { area },
                BaseObjects = new List<GameObject> { countryBaseObject },
                GameObjects = new List<GameObject> { countryGameObject }
            };

            var planetBaseObject = new GameObject { Name = "Planet Base Relic" };
            var planetGameObject = new GameObject { Name = "Planet Relic" };
            var planet = new Planet
            {
                Name = "Planet",
                Countries = new List<Country> { country },
                BaseObjects = new List<GameObject> { planetBaseObject },
                GameObjects = new List<GameObject> { planetGameObject }
            };

            var project = new ProjectModel
            {
                Name = "ScopedCleanObjects",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "ScopedCleanObjects.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            Assert.False(projectDoc.RootElement.TryGetProperty("planets", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("baseObjects", out _));
            Assert.Equal(planet.Id, projectDoc.RootElement.GetProperty("planetIds").EnumerateArray().Single().GetGuid());
            Assert.False(projectDoc.RootElement.TryGetProperty("baseObjectIds", out _));

            var planetObjectId = planetGameObject.ObjectId;
            var countryObjectId = countryGameObject.ObjectId;
            var areaObjectId = areaGameObject.ObjectId;
            var planetBaseObjectId = planetBaseObject.ObjectId;
            var countryBaseObjectId = countryBaseObject.ObjectId;
            var areaBaseObjectId = areaBaseObject.ObjectId;

            using var scopeKindPlanetDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindPlanetNodePath(projectFilePath, planet.Id), Encoding.UTF8));
            using var scopeKindCountryDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindCountryNodePath(projectFilePath, country.Id), Encoding.UTF8));
            using var scopeKindAreaDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindAreaNodePath(projectFilePath, area.Id), Encoding.UTF8));

            Assert.False(scopeKindPlanetDoc.RootElement.TryGetProperty("gameObjects", out _));
            Assert.False(scopeKindCountryDoc.RootElement.TryGetProperty("gameObjects", out _));
            Assert.False(scopeKindAreaDoc.RootElement.TryGetProperty("gameObjects", out _));
            Assert.False(scopeKindPlanetDoc.RootElement.TryGetProperty("baseObjects", out _));
            Assert.False(scopeKindCountryDoc.RootElement.TryGetProperty("baseObjects", out _));
            Assert.False(scopeKindAreaDoc.RootElement.TryGetProperty("baseObjects", out _));

            Assert.Equal(country.Id, scopeKindPlanetDoc.RootElement.GetProperty("countryIds").EnumerateArray().Single().GetGuid());
            Assert.False(scopeKindPlanetDoc.RootElement.TryGetProperty("countries", out _));
            Assert.Equal(area.Id, scopeKindCountryDoc.RootElement.GetProperty("areaIds").EnumerateArray().Single().GetGuid());
            Assert.False(scopeKindCountryDoc.RootElement.TryGetProperty("areas", out _));

            var scopeKindPlanetGameObjectIds = scopeKindPlanetDoc.RootElement.GetProperty("gameObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            var scopeKindCountryGameObjectIds = scopeKindCountryDoc.RootElement.GetProperty("gameObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            var scopeKindAreaGameObjectIds = scopeKindAreaDoc.RootElement.GetProperty("gameObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            var scopeKindPlanetBaseObjectIds = scopeKindPlanetDoc.RootElement.GetProperty("baseObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            var scopeKindCountryBaseObjectIds = scopeKindCountryDoc.RootElement.GetProperty("baseObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            var scopeKindAreaBaseObjectIds = scopeKindAreaDoc.RootElement.GetProperty("baseObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();

            Assert.Contains(planetObjectId, scopeKindPlanetGameObjectIds);
            Assert.Contains(countryObjectId, scopeKindCountryGameObjectIds);
            Assert.Contains(areaObjectId, scopeKindAreaGameObjectIds);
            Assert.Contains(planetBaseObjectId, scopeKindPlanetBaseObjectIds);
            Assert.Contains(countryBaseObjectId, scopeKindCountryBaseObjectIds);
            Assert.Contains(areaBaseObjectId, scopeKindAreaBaseObjectIds);

            Assert.True(File.Exists(BuildScopeKindGameObjectNodePath(projectFilePath, planetObjectId)));
            Assert.True(File.Exists(BuildScopeKindGameObjectNodePath(projectFilePath, countryObjectId)));
            Assert.True(File.Exists(BuildScopeKindGameObjectNodePath(projectFilePath, areaObjectId)));
            Assert.True(File.Exists(BuildScopeKindGameObjectNodePath(projectFilePath, planetBaseObjectId)));
            Assert.True(File.Exists(BuildScopeKindGameObjectNodePath(projectFilePath, countryBaseObjectId)));
            Assert.True(File.Exists(BuildScopeKindGameObjectNodePath(projectFilePath, areaBaseObjectId)));

            using var planetGameObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, planetObjectId), Encoding.UTF8));
            using var countryGameObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, countryObjectId), Encoding.UTF8));
            using var areaGameObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, areaObjectId), Encoding.UTF8));
            using var planetBaseObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, planetBaseObjectId), Encoding.UTF8));
            using var countryBaseObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, countryBaseObjectId), Encoding.UTF8));
            using var areaBaseObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, areaBaseObjectId), Encoding.UTF8));

            Assert.Equal("Planet Relic", planetGameObjectDoc.RootElement.GetProperty("name").GetString());
            Assert.Equal("Country Relic", countryGameObjectDoc.RootElement.GetProperty("name").GetString());
            Assert.Equal("Area Relic", areaGameObjectDoc.RootElement.GetProperty("name").GetString());
            Assert.Equal("Planet Base Relic", planetBaseObjectDoc.RootElement.GetProperty("name").GetString());
            Assert.Equal("Country Base Relic", countryBaseObjectDoc.RootElement.GetProperty("name").GetString());
            Assert.Equal("Area Base Relic", areaBaseObjectDoc.RootElement.GetProperty("name").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_IncludesObjectRenderCoordinateProperties_InCleanGameProperties()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var gameObject = new GameObject
            {
                Name = "PlacedObject",
                PositionX = 111.5,
                PositionY = 222.25,
                ImageRotationDegrees = 180,
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = "images/object.png",
                        ImageScale = 1.75,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room { Name = "Coordinate Room", GameObjects = new List<GameObject> { gameObject } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "CleanObjectCoordinates", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "CleanObjectCoordinates.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var exportedObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var gameProperties = exportedObject.GetProperty("gameProperties").EnumerateArray().ToList();

            string GetDefaultValue(string propertyName)
            {
                return gameProperties
                    .Single(property => string.Equals(property.GetProperty("name").GetString(), propertyName, StringComparison.OrdinalIgnoreCase))
                    .GetProperty("defaultValue")
                    .GetString() ?? string.Empty;
            }

            Assert.Equal("111.5", GetDefaultValue("positionX"));
            Assert.Equal("222.25", GetDefaultValue("positionY"));
            Assert.Equal("180", GetDefaultValue("imageRotationDegrees"));
            Assert.Equal("1.75", GetDefaultValue("imageScale"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_PreservesSharedVariableId_ForObjectGameProperties()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sharedVariableId = Guid.NewGuid();
            var gameObject = new GameObject
            {
                Name = "SharedDoor",
                Variables = new List<GamePropertyDefinition>
                {
                    new()
                    {
                        Name = "isOpen",
                        DefaultValue = "false",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                        Lifetime = GamePropertyLifetime.Singleton,
                        SharedVariableId = sharedVariableId
                    }
                }
            };

            var room = new Room { Name = "Room", GameObjects = new List<GameObject> { gameObject } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "CleanSharedVariable", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "CleanSharedVariable.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var exportedObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var exportedProperty = exportedObject
                .GetProperty("gameProperties")
                .EnumerateArray()
                .Single(property => string.Equals(property.GetProperty("name").GetString(), "isOpen", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(sharedVariableId, exportedProperty.GetProperty("sharedVariableId").GetGuid());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_OmitsEmptyOptionalRoomFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Minimal",
                Description = "",
                ProducerNotes = ""
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "MinimalProject", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "MinimalProject.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var roomJson = File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8);

            Assert.DoesNotContain("\"producerNotes\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"additionalVerbs\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"additionalDirectionals\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"gameProperties\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"objects\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"images\"", roomJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_OmitsDefaultAuthoredAndOccupancyScaffoldingFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var gameObject = new GameObject
            {
                Name = "DefaultScaffoldingObject",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = string.Empty,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room { Name = "Room", GameObjects = new List<GameObject> { gameObject } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "CleanDefaultScaffolding", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "CleanDefaultScaffolding.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var roomJson = File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8);

            Assert.DoesNotContain("\"authoredRenderOrder\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"authoredBaseHeightInRoom\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"isHeightPinned\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"occupiedCellIds\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"occupancyDerivationSourceEcho\"", roomJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_EmitsRelativeImagePaths_AcrossProjectRoomAndManifestCarriers()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceImagesPath = Path.Combine(tempRoot, "source");
            Directory.CreateDirectory(sourceImagesPath);

            var imagePath = WriteTinyPng(sourceImagesPath, "portable.png");

            var globalObject = new GameObject
            {
                Name = "GlobalDisplay",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = imagePath,
                        IsDefault = true
                    }
                ]
            };

            var roomObject = new GameObject
            {
                Name = "RoomDisplay",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = imagePath,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room
            {
                Name = "Visual Room",
                GameObjects = new List<GameObject> { roomObject },
                Images =
                [
                    new RoomImageEntry
                    {
                        Slot = RoomImageSlot.North,
                        Image = new RoomImageVariant
                        {
                            FullImagePath = imagePath,
                            GrayMapImagePath = imagePath,
                            NormalMapImagePath = imagePath
                        }
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "PortablePaths",
                GameObjects = new List<GameObject> { globalObject },
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "PortablePaths.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            var globalObjectId = projectDoc.RootElement
                .GetProperty("gameObjectIds")
                .EnumerateArray()
                .Single()
                .GetGuid();
            using var globalObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, globalObjectId), Encoding.UTF8));
            var projectVariantPath = globalObjectDoc.RootElement
                .GetProperty("imageVariants")
                .EnumerateArray()
                .Single()
                .GetProperty("fullImagePath")
                .GetString();

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var exportedRoomObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var roomVariantPath = exportedRoomObject
                .GetProperty("imageVariants")
                .EnumerateArray()
                .Single()
                .GetProperty("fullImagePath")
                .GetString();
            var roomImagePath = roomDoc.RootElement
                .GetProperty("images")
                .EnumerateArray()
                .Single()
                .GetProperty("fullImagePath")
                .GetString();

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(BuildAssetsManifestPath(projectFilePath), Encoding.UTF8));
            var manifestExportedPath = manifestDoc.RootElement
                .GetProperty("images")
                .EnumerateArray()
                .Single()
                .GetProperty("exportedPath")
                .GetString();

            AssertRelativeCleanAssetPath(projectVariantPath);
            AssertRelativeCleanAssetPath(roomVariantPath);
            AssertRelativeCleanAssetPath(roomImagePath);
            AssertRelativeCleanAssetPath(manifestExportedPath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_EmitsConsistentImagePathSemantics_AcrossAllImageCarriers()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceImagesPath = Path.Combine(tempRoot, "source");
            Directory.CreateDirectory(sourceImagesPath);

            var imagePath = WriteTinyPng(sourceImagesPath, "semantics.png");

            var globalObject = new GameObject
            {
                Name = "GlobalDisplay",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = imagePath,
                        IsDefault = true
                    }
                ]
            };

            var roomObject = new GameObject
            {
                Name = "RoomDisplay",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = imagePath,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room
            {
                Name = "Visual Room",
                GameObjects = new List<GameObject> { roomObject },
                Images =
                [
                    new RoomImageEntry
                    {
                        Slot = RoomImageSlot.North,
                        Image = new RoomImageVariant
                        {
                            FullImagePath = imagePath
                        }
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "SemanticsConsistency",
                GameObjects = new List<GameObject> { globalObject },
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "SemanticsConsistency.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            var globalObjectId = projectDoc.RootElement
                .GetProperty("gameObjectIds")
                .EnumerateArray()
                .Single()
                .GetGuid();
            using var globalObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, globalObjectId), Encoding.UTF8));
            var projectVariantSemantics = globalObjectDoc.RootElement
                .GetProperty("imageVariants")
                .EnumerateArray()
                .Single()
                .GetProperty("imagePathSemantics")
                .GetString();

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var exportedRoomObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var roomVariantSemantics = exportedRoomObject
                .GetProperty("imageVariants")
                .EnumerateArray()
                .Single()
                .GetProperty("imagePathSemantics")
                .GetString();
            var roomImageSemantics = roomDoc.RootElement
                .GetProperty("images")
                .EnumerateArray()
                .Single()
                .GetProperty("imagePathSemantics")
                .GetString();

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(BuildAssetsManifestPath(projectFilePath), Encoding.UTF8));
            var manifestImageSemantics = manifestDoc.RootElement
                .GetProperty("images")
                .EnumerateArray()
                .Single()
                .GetProperty("imagePathSemantics")
                .GetString();

            Assert.Equal("runtimeExportRelative", projectVariantSemantics);
            Assert.Equal(projectVariantSemantics, roomVariantSemantics);
            Assert.Equal(projectVariantSemantics, roomImageSemantics);
            Assert.Equal(projectVariantSemantics, manifestImageSemantics);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_WritesOutcomeMessageMap_WithSupportedBlankEntriesAndCanonicalTokens()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var action = new CommandAction
            {
                Name = "Set",
                ActionType = CommandActionType.SetGameProperty,
                GamePropertyName = "doorState",
                GamePropertyValue = "open",
                OutcomeMessageMap = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["success"] = string.Empty,
                    ["CustomFutureCode"] = "future text"
                }
            };

            var room = new Room { Name = "Room", AvailableActions = [action] };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "OutcomeMapClean", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "OutcomeMapClean.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var exportedAction = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().Single();
            var outcomeMap = exportedAction.GetProperty("outcomeMessageMap");

            Assert.Equal(string.Empty, outcomeMap.GetProperty("Success").GetString());
            Assert.Equal("future text", outcomeMap.GetProperty("CustomFutureCode").GetString());
            Assert.False(outcomeMap.TryGetProperty("Failure", out _));
            Assert.False(outcomeMap.TryGetProperty("success", out _));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_EmitsProcedureId_ForInvokeProcedureActions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var procedureId = Guid.NewGuid();
            var action = new CommandAction
            {
                Name = "Invoke",
                ActionType = CommandActionType.InvokeProcedure,
                Payload = new InvokeProcedurePayload(procedureId)
            };

            var room = new Room { Name = "Room", AvailableActions = [action] };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "InvokeProcedureClean", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "InvokeProcedureClean.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var exportedAction = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().Single();
            Assert.Equal(procedureId.ToString(), exportedAction.GetProperty("payload").GetProperty("procedureId").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_WritesProcedureSidecars_AndProcedureIds_OnRuntimeRoot()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var procedureId = Guid.NewGuid();
            var project = new ProjectModel
            {
                Name = "RuntimeProcedureSidecars",
                Procedures =
                [
                    new ProcedureDefinition
                    {
                        Id = procedureId,
                        Name = "FixDoor",
                        ProcedureSummary = "Fixes the jammed door",
                        ProcedureDescription = "Applies the repair sequence to the stuck door."
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "RuntimeProcedureSidecars.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            Assert.True(projectDoc.RootElement.TryGetProperty("procedureIds", out var procedureIdsElement));
            var procedureIds = procedureIdsElement
                .EnumerateArray()
                .Select(static value => value.GetGuid())
                .ToList();
            Assert.Single(procedureIds);
            Assert.Equal(procedureId, procedureIds[0]);
            Assert.False(projectDoc.RootElement.TryGetProperty("procedures", out _));

            var procedurePath = BuildRuntimeProcedurePath(projectFilePath, procedureId);
            Assert.True(File.Exists(procedurePath));

            using var procedureDoc = JsonDocument.Parse(File.ReadAllText(procedurePath, Encoding.UTF8));
            Assert.Equal(procedureId, procedureDoc.RootElement.GetProperty("id").GetGuid());
            Assert.Equal("FixDoor", procedureDoc.RootElement.GetProperty("name").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_StoresLinkedRoomInstancePointers_WithoutDuplicatingBaseActions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var baseAction = new CommandAction
            {
                Name = "baseLook",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "base action output",
                Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                Verbs = new List<string> { "look" }
            };

            var staleAction = new CommandAction
            {
                Name = "staleLocal",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "stale local output",
                Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                Verbs = new List<string> { "look" }
            };

            var baseObject = new GameObject
            {
                Name = "marbles",
                IsQuantifiable = true,
                Quantity = 1,
                QuantifiablePlacementDistributionMode = "GroupedStack",
                AvailableActions = new List<CommandAction> { baseAction }
            };

            var roomInstance = new GameObject
            {
                Name = "marbles",
                IsQuantifiable = true,
                Quantity = 2,
                QuantifiablePlacementDistributionMode = "GroupedStack",
                LinkedBaseObjectId = baseObject.ObjectId,
                LinkActionsToBaseObject = true,
                AvailableActions = new List<CommandAction> { staleAction }
            };

            var room = new Room { Name = "Room", GameObjects = new List<GameObject> { roomInstance } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "LinkedExport",
                Planets = new List<Planet> { planet },
                BaseObjects = new List<GameObject> { baseObject }
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedExport.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var cleanRoomPath = BuildRuntimeRoomPath(projectFilePath, room.Id);
            var roomJson = File.ReadAllText(cleanRoomPath, Encoding.UTF8);
            var cleanProjectPath = BuildRuntimeProjectPath(projectFilePath);
            var projectJson = File.ReadAllText(cleanProjectPath, Encoding.UTF8);

            Assert.DoesNotContain("base action output", roomJson, StringComparison.Ordinal);
            Assert.DoesNotContain("stale local output", roomJson, StringComparison.Ordinal);

            var exportedRoomObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            Assert.Equal("marbles", exportedRoomObject.GetProperty("name").GetString());
            Assert.True(exportedRoomObject.TryGetProperty("linkedBaseObjectId", out var linkedBaseId));
            Assert.Equal(baseObject.ObjectId, linkedBaseId.GetGuid());
            Assert.True(exportedRoomObject.TryGetProperty("linkActionsToBaseObject", out var linkActionsFlag));
            Assert.True(linkActionsFlag.GetBoolean());

            using var projectDoc = JsonDocument.Parse(projectJson);
            var projectBaseObjectIds = projectDoc.RootElement.GetProperty("baseObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            Assert.Contains(baseObject.ObjectId, projectBaseObjectIds);
            Assert.False(projectDoc.RootElement.TryGetProperty("planets", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("baseObjects", out _));

            using var baseObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, baseObject.ObjectId), Encoding.UTF8));
            var exportedBaseObject = baseObjectDoc.RootElement;
            var exportedBaseActions = exportedBaseObject.GetProperty("availableGameActions");
            Assert.Equal(JsonValueKind.Array, exportedBaseActions.ValueKind);
            Assert.Single(exportedBaseActions.EnumerateArray());
            Assert.Contains("base action output", exportedBaseObject.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_LinkedObject_OmitsDefinitionOwnedVisualAndAppearanceFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var baseObjectId = Guid.NewGuid();
            var baseObject = new GameObject
            {
                ObjectId = baseObjectId,
                Name = "Base Crate",
                ImageVariantChooserScript = "return 'base-night';",
                ImageVariants =
                [
                    new ObjectImageVariant { VariantName = "base-day", FullImagePath = "images/base-day.png", IsDefault = true },
                    new ObjectImageVariant { VariantName = "base-night", FullImagePath = "images/base-night.png" }
                ],
                StackGroup = 7,
                FootprintWidthCells = 3,
                FootprintHeightCells = 2,
                FootprintOrientation = "W",
                ObjectHeightUnits = 4,
                StackScaleStepOverride = 0.15,
                MinStackScaleOverride = 0.55
            };

            var linked = new GameObject
            {
                Name = "Legacy Crate",
                LinkedBaseObjectId = baseObjectId,
                LinkActionsToBaseObject = true,
                ImageVariantChooserScript = "return 'stale-local';",
                ImageVariants =
                [
                    new ObjectImageVariant { VariantName = "stale-local", FullImagePath = "images/stale.png", IsDefault = true }
                ],
                StackGroup = 1,
                FootprintWidthCells = 1,
                FootprintHeightCells = 1,
                FootprintOrientation = "N",
                ObjectHeightUnits = 1,
                StackScaleStepOverride = 0.01,
                MinStackScaleOverride = 0.10
            };

            var room = new Room { Name = "Room", GameObjects = new List<GameObject> { linked } };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "LinkedVisualEffectiveExport",
                BaseObjects = new List<GameObject> { baseObject },
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedVisualEffectiveExport.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var exported = LoadSingleRoomExportedObject(projectFilePath, room.Id);

            Assert.Equal("Legacy Crate", exported.GetProperty("name").GetString());
            Assert.True(exported.TryGetProperty("linkedBaseObjectId", out var linkedBaseId));
            Assert.Equal(baseObjectId, linkedBaseId.GetGuid());
            Assert.True(exported.TryGetProperty("linkActionsToBaseObject", out var linkActionsFlag));
            Assert.True(linkActionsFlag.GetBoolean());

            Assert.False(exported.TryGetProperty("imageVariants", out _));
            Assert.False(exported.TryGetProperty("imageVariantChooserScript", out _));
            Assert.False(exported.TryGetProperty("appearance", out _));
            Assert.Equal("Legacy Crate", exported.GetProperty("nameInGame").GetString());
            Assert.False(exported.TryGetProperty("nameSynonyms", out _));
            Assert.False(exported.TryGetProperty("description", out _));

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            var projectBaseObjectIds = projectDoc.RootElement.GetProperty("baseObjectIds").EnumerateArray().Select(id => id.GetGuid()).ToList();
            Assert.Contains(baseObjectId, projectBaseObjectIds);
            Assert.False(projectDoc.RootElement.TryGetProperty("baseObjects", out _));

            using var baseObjectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, baseObjectId), Encoding.UTF8));
            var exportedBaseObject = baseObjectDoc.RootElement;
            Assert.True(exportedBaseObject.TryGetProperty("imageVariants", out _));
            Assert.True(exportedBaseObject.TryGetProperty("imageVariantChooserScript", out _));
            Assert.True(exportedBaseObject.TryGetProperty("appearance", out _));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_DedupesImagesByContentHash_AndWritesManifest()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceImagesPath = Path.Combine(tempRoot, "source");
            Directory.CreateDirectory(sourceImagesPath);

            var imageA = WriteTinyPng(sourceImagesPath, "same-a.png");
            var imageB = WriteTinyPng(sourceImagesPath, "same-b.png");

            var gameObject = new GameObject
            {
                Name = "DisplayObject",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = imageA,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room
            {
                Name = "Visual Room",
                GameObjects = new List<GameObject> { gameObject },
                Images = new List<RoomImageEntry>
                {
                    new()
                    {
                        Slot = RoomImageSlot.North,
                        OverlayOffsetX = 12.5,
                        OverlayOffsetY = -3.25,
                        OverlayRotationDegrees = 22,
                        Image = new RoomImageVariant
                        {
                            FullImagePath = imageB
                        }
                    }
                }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "CleanObjectAppearance", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "CleanObjectAppearance.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var exportedObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var exportedRoomImage = roomDoc.RootElement.GetProperty("images").EnumerateArray().Single();

            var objectImagePath = exportedObject.GetProperty("imageVariants").EnumerateArray().Single().GetProperty("fullImagePath").GetString();
            var roomImagePath = exportedRoomImage.GetProperty("fullImagePath").GetString();

            Assert.Equal(objectImagePath, roomImagePath);
            Assert.Equal("runtimeExportRelative", exportedObject.GetProperty("imageVariants").EnumerateArray().Single().GetProperty("imagePathSemantics").GetString());
            Assert.Equal("runtimeExportRelative", exportedRoomImage.GetProperty("imagePathSemantics").GetString());
            Assert.Equal(12.5, exportedRoomImage.GetProperty("overlayOffsetX").GetDouble(), 3);
            Assert.Equal(-3.25, exportedRoomImage.GetProperty("overlayOffsetY").GetDouble(), 3);
            Assert.Equal(22, exportedRoomImage.GetProperty("overlayRotationDegrees").GetDouble(), 3);

            var sharedImageFiles = Directory.EnumerateFiles(BuildCleanSharedImageFolderPath(projectFilePath), "*.png", SearchOption.TopDirectoryOnly).ToList();
            Assert.Single(sharedImageFiles);

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(BuildAssetsManifestPath(projectFilePath), Encoding.UTF8));
            Assert.Equal("1.0", manifestDoc.RootElement.GetProperty("schemaVersion").GetString());

            var images = manifestDoc.RootElement.GetProperty("images").EnumerateArray().ToList();
            Assert.Single(images);

            var manifestImage = images[0];
            Assert.Equal("runtimeExportRelative", manifestImage.GetProperty("imagePathSemantics").GetString());
            Assert.Equal(objectImagePath, manifestImage.GetProperty("exportedPath").GetString());
            Assert.Equal(manifestImage.GetProperty("hash").GetString(), manifestImage.GetProperty("sha256").GetString());
            Assert.True(manifestImage.GetProperty("sizeBytes").GetInt64() > 0);
            Assert.Equal(2, manifestImage.GetProperty("sourcePaths").EnumerateArray().Count());
            Assert.Equal(2, manifestImage.GetProperty("references").EnumerateArray().Count());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_StagesProjectPreviewImages_UnderPreviewImagesFolder()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceImagesPath = Path.Combine(tempRoot, "source");
            Directory.CreateDirectory(sourceImagesPath);

            var previewImagePath = WriteTinyPng(sourceImagesPath, "cover.png");
            var missingPreviewImagePath = Path.Combine(sourceImagesPath, "missing-cover.png");

            var project = new ProjectModel
            {
                Name = "PreviewImageStaging",
                GameDisplayName = "Preview Display Name",
                GameSummary = "Preview summary text.",
                GamePreviewImages =
                [
                    previewImagePath,
                    missingPreviewImagePath
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "PreviewImageStaging.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            Assert.Equal("Preview Display Name", projectDoc.RootElement.GetProperty("gameDisplayName").GetString());
            Assert.Equal("Preview summary text.", projectDoc.RootElement.GetProperty("gameSummary").GetString());

            var exportedPreviewImages = projectDoc.RootElement
                .GetProperty("gamePreviewImages")
                .EnumerateArray()
                .Select(static value => value.GetString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var exportedPreviewImage = Assert.Single(exportedPreviewImages);
            AssertRelativeCleanAssetPath(exportedPreviewImage);
            Assert.StartsWith("assets/images/previewImages/", exportedPreviewImage, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(BuildRuntimeExportRootPath(projectFilePath), exportedPreviewImage!.Replace('/', Path.DirectorySeparatorChar))));

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(BuildAssetsManifestPath(projectFilePath), Encoding.UTF8));
            var manifestImages = manifestDoc.RootElement.GetProperty("images").EnumerateArray().ToList();
            Assert.Contains(manifestImages, image => string.Equals(
                image.GetProperty("exportedPath").GetString(),
                exportedPreviewImage,
                StringComparison.OrdinalIgnoreCase));

            var unresolvedSources = manifestDoc.RootElement
                .GetProperty("unresolvedSources")
                .EnumerateArray()
                .Select(static value => value.GetString())
                .ToList();
            Assert.Contains(missingPreviewImagePath, unresolvedSources, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_StagesSoundAssets_AndWritesSoundManifestEntries()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceSoundsPath = Path.Combine(tempRoot, "sound-source");
            Directory.CreateDirectory(sourceSoundsPath);

            var soundA = WriteTinyWav(sourceSoundsPath, "tone-a.wav", 0x11);
            var soundB = WriteTinyWav(sourceSoundsPath, "tone-b.wav", 0x11);

            var room = new Room
            {
                Name = "Sound Room",
                SoundEffectLibraryEntries =
                [
                    new SoundEffectLibraryEntry
                    {
                        SoundEffectId = Guid.NewGuid(),
                        SoundEffectKey = "room-tone",
                        DisplayName = "Room Tone",
                        AssetRef = soundB
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "SoundStaging",
                Planets = new List<Planet> { planet }
            };

            project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
            {
                SoundEffectId = Guid.NewGuid(),
                SoundEffectKey = "global-tone",
                DisplayName = "Global Tone",
                AssetRef = soundA
            });

            var projectFilePath = Path.Combine(tempRoot, "SoundStaging.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            var projectSoundAssetRef = projectDoc.RootElement
                .GetProperty("soundEffectLibraryEntries")
                .EnumerateArray()
                .Single()
                .GetProperty("assetRef")
                .GetString();

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var roomSoundAssetRef = roomDoc.RootElement
                .GetProperty("soundEffectLibraryEntries")
                .EnumerateArray()
                .Single()
                .GetProperty("assetRef")
                .GetString();

            AssertRelativeCleanAssetPath(projectSoundAssetRef);
            Assert.StartsWith("assets/sounds/_shared/", projectSoundAssetRef, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(projectSoundAssetRef, roomSoundAssetRef);
            Assert.True(File.Exists(Path.Combine(BuildRuntimeExportRootPath(projectFilePath), projectSoundAssetRef!.Replace('/', Path.DirectorySeparatorChar))));

            var sharedSoundFiles = Directory.EnumerateFiles(BuildCleanSharedSoundFolderPath(projectFilePath), "*.wav", SearchOption.TopDirectoryOnly).ToList();
            Assert.Single(sharedSoundFiles);

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(BuildAssetsManifestPath(projectFilePath), Encoding.UTF8));
            var sounds = manifestDoc.RootElement.GetProperty("sounds").EnumerateArray().ToList();
            Assert.Single(sounds);
            Assert.Equal("runtimeExportRelative", sounds[0].GetProperty("assetRefSemantics").GetString());
            Assert.Equal(projectSoundAssetRef, sounds[0].GetProperty("exportedPath").GetString());
            Assert.Equal(sounds[0].GetProperty("hash").GetString(), sounds[0].GetProperty("sha256").GetString());
            Assert.True(sounds[0].GetProperty("sizeBytes").GetInt64() > 0);
            Assert.Equal(2, sounds[0].GetProperty("references").EnumerateArray().Count());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_ResolvesNamedAssetRootExpressions_ForImagesAndSounds()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var prior = Environment.GetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable);
        var namedPrior = Environment.GetEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_SHARED");
        try
        {
            var primaryRoot = Path.Combine(tempRoot, "primary-assets");
            var namedRoot = Path.Combine(tempRoot, "shared-assets");
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, primaryRoot);
            Environment.SetEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_SHARED", namedRoot);

            var imageSourceFolder = Path.Combine(primaryRoot, "FormalImages");
            var soundSourceFolder = Path.Combine(namedRoot, "PlaceHolderSounds");
            Directory.CreateDirectory(imageSourceFolder);
            Directory.CreateDirectory(soundSourceFolder);

            var imagePath = WriteTinyPng(imageSourceFolder, "token-image.png");
            var soundPath = WriteTinyWav(soundSourceFolder, "token-sound.wav", 0x31);

            var room = new Room
            {
                Name = "Token Room",
                Images =
                [
                    new RoomImageEntry
                    {
                        Slot = RoomImageSlot.North,
                        Image = new RoomImageVariant
                        {
                            FullImagePath = "%STORYBOARD_ASSET_SOURCE_ROOT%/FormalImages/token-image.png"
                        }
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "TokenExport",
                Planets = new List<Planet> { planet }
            };

            project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
            {
                SoundEffectId = Guid.NewGuid(),
                SoundEffectKey = "token-sound",
                AssetRef = "%STORYBOARD_ASSET_SOURCE_ROOT_SHARED%/PlaceHolderSounds/token-sound.wav"
            });

            var projectFilePath = Path.Combine(tempRoot, "TokenExport.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            Assert.True(File.Exists(imagePath));
            Assert.True(File.Exists(soundPath));

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(BuildAssetsManifestPath(projectFilePath), Encoding.UTF8));
            var imageSources = manifestDoc.RootElement
                .GetProperty("images")
                .EnumerateArray()
                .Single()
                .GetProperty("sourcePaths")
                .EnumerateArray()
                .Select(static value => value.GetString())
                .ToList();
            Assert.Contains("%STORYBOARD_ASSET_SOURCE_ROOT%/FormalImages/token-image.png", imageSources);

            var soundSources = manifestDoc.RootElement
                .GetProperty("sounds")
                .EnumerateArray()
                .Single()
                .GetProperty("sourcePaths")
                .EnumerateArray()
                .Select(static value => value.GetString())
                .ToList();
            Assert.Contains("%STORYBOARD_ASSET_SOURCE_ROOT_SHARED%/PlaceHolderSounds/token-sound.wav", soundSources);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, prior);
            Environment.SetEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_SHARED", namedPrior);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_StagesPresentationCueCatalog_UnderPresentationCuesFolder()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room { Name = "Cue Room" };
            var area = new Area { Name = "Cue Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Cue Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Cue Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "CueCatalogStaging",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "CueCatalogStaging.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var sourceCatalogPath = BuildExpectedPresentationCueCatalogSourcePath();
            var exportedCatalogPath = BuildRuntimePresentationCueCatalogPath(projectFilePath);
            var exportedRelativePath = Path.GetRelativePath(BuildRuntimeExportRootPath(projectFilePath), exportedCatalogPath)
                .Replace('\\', '/');

            Assert.True(File.Exists(sourceCatalogPath));
            Assert.True(File.Exists(exportedCatalogPath));
            Assert.Equal("assets/PresentationCues/presentation-effects.catalog.json", exportedRelativePath);
            Assert.Equal(
                File.ReadAllText(sourceCatalogPath, Encoding.UTF8),
                File.ReadAllText(exportedCatalogPath, Encoding.UTF8));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_NormalizesSoundEffectRepeatFields_ByRepeatMode()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Sound Room",
                SoundEffectLibraryEntries =
                [
                    new SoundEffectLibraryEntry
                    {
                        SoundEffectId = Guid.NewGuid(),
                        SoundEffectKey = "room-count",
                        DisplayName = "Room Count",
                        AssetRef = "room-count.wav",
                        RepeatMode = "RepeatCount",
                        RepeatCount = 3,
                        RepeatDurationMs = 6000,
                        RepeatIntervalMs = 120,
                        RepeatCooldownMs = 180
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "SoundRepeatRuntimeNormalization",
                Planets = new List<Planet> { planet }
            };

            project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
            {
                SoundEffectId = Guid.NewGuid(),
                SoundEffectKey = "global-none",
                DisplayName = "Global None",
                AssetRef = "global-none.wav",
                RepeatMode = "None",
                RepeatCount = 5,
                RepeatDurationMs = 7000,
                RepeatIntervalMs = 300,
                RepeatCooldownMs = 600
            });

            var projectFilePath = Path.Combine(tempRoot, "SoundRepeatRuntimeNormalization.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using (var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8)))
            {
                var globalEntry = projectDoc.RootElement
                    .GetProperty("soundEffectLibraryEntries")
                    .EnumerateArray()
                    .Single();

                Assert.Equal("None", globalEntry.GetProperty("repeatMode").GetString());
                Assert.False(globalEntry.TryGetProperty("repeatCount", out _));
                Assert.False(globalEntry.TryGetProperty("repeatDurationMs", out _));
                Assert.False(globalEntry.TryGetProperty("repeatIntervalMs", out _));
                Assert.False(globalEntry.TryGetProperty("repeatCooldownMs", out _));
            }

            using (var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8)))
            {
                var roomEntry = roomDoc.RootElement
                    .GetProperty("soundEffectLibraryEntries")
                    .EnumerateArray()
                    .Single();

                Assert.Equal("RepeatCount", roomEntry.GetProperty("repeatMode").GetString());
                Assert.Equal(3, roomEntry.GetProperty("repeatCount").GetInt32());
                Assert.False(roomEntry.TryGetProperty("repeatDurationMs", out _));
                Assert.Equal(120, roomEntry.GetProperty("repeatIntervalMs").GetInt32());
                Assert.Equal(180, roomEntry.GetProperty("repeatCooldownMs").GetInt32());
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_NormalizesSoundEffectRepeatFields_UntilCanceled()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Sound Room",
                SoundEffectLibraryEntries =
                [
                    new SoundEffectLibraryEntry
                    {
                        SoundEffectId = Guid.NewGuid(),
                        SoundEffectKey = "room-until-canceled",
                        DisplayName = "Room Until Canceled",
                        AssetRef = "room-until-canceled.wav",
                        RepeatMode = "UntilCanceled",
                        RepeatCount = 9,
                        RepeatDurationMs = 6000,
                        RepeatIntervalMs = 120,
                        RepeatCooldownMs = 180
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "SoundRepeatUntilCanceledRuntimeNormalization",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "SoundRepeatUntilCanceledRuntimeNormalization.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var roomEntry = roomDoc.RootElement
                .GetProperty("soundEffectLibraryEntries")
                .EnumerateArray()
                .Single();

            Assert.Equal("UntilCanceled", roomEntry.GetProperty("repeatMode").GetString());
            Assert.False(roomEntry.TryGetProperty("repeatCount", out _));
            Assert.False(roomEntry.TryGetProperty("repeatDurationMs", out _));
            Assert.Equal(120, roomEntry.GetProperty("repeatIntervalMs").GetInt32());
            Assert.Equal(180, roomEntry.GetProperty("repeatCooldownMs").GetInt32());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_UnresolvedImageSources_EmitEmptyPayloadPaths_AndManifestUnresolvedSources()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var missingImagePath = Path.Combine(tempRoot, "missing", "does-not-exist.png");

            var roomObject = new GameObject
            {
                Name = "RoomDisplay",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = missingImagePath,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room
            {
                Name = "Visual Room",
                GameObjects = new List<GameObject> { roomObject },
                Images =
                [
                    new RoomImageEntry
                    {
                        Slot = RoomImageSlot.North,
                        Image = new RoomImageVariant
                        {
                            FullImagePath = missingImagePath,
                            GrayMapImagePath = missingImagePath,
                            NormalMapImagePath = missingImagePath
                        }
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel { Name = "MissingImages", Planets = new List<Planet> { planet } };

            var projectFilePath = Path.Combine(tempRoot, "MissingImages.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, room.Id), Encoding.UTF8));
            var exportedObject = LoadSingleRoomExportedObject(projectFilePath, room.Id);
            var exportedVariant = exportedObject.GetProperty("imageVariants").EnumerateArray().Single();
            var exportedRoomImage = roomDoc.RootElement.GetProperty("images").EnumerateArray().Single();

            Assert.Equal(string.Empty, exportedVariant.GetProperty("fullImagePath").GetString());
            Assert.Equal(string.Empty, exportedRoomImage.GetProperty("fullImagePath").GetString());
            Assert.Equal(string.Empty, exportedRoomImage.GetProperty("grayMapImagePath").GetString());
            Assert.Equal(string.Empty, exportedRoomImage.GetProperty("normalMapImagePath").GetString());

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(BuildAssetsManifestPath(projectFilePath), Encoding.UTF8));
            var unresolvedSources = manifestDoc.RootElement.GetProperty("unresolvedSources").EnumerateArray().Select(p => p.GetString()).ToList();

            Assert.Contains(unresolvedSources, path =>
                !string.IsNullOrWhiteSpace(path)
                && path.EndsWith(missingImagePath, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, unresolvedSources.Count(path =>
                !string.IsNullOrWhiteSpace(path)
                && path.EndsWith(missingImagePath, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_EmitsDirectionalTraversalMappings_ForSupportedScopeNodes()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var scopedObject = new GameObject
            {
                Name = "Scoped Object",
                AdditionalDirectionalTraversalMappings =
                [
                    new DirectionalTraversalMapping { Token = "object-bias", TraversalDirection = Direction10.West }
                ]
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [scopedObject],
                AdditionalDirectionalTraversalMappings =
                [
                    new DirectionalTraversalMapping { Token = "room-bias", TraversalDirection = Direction10.SouthEast }
                ]
            };

            var area = new Area
            {
                Name = "Area",
                Rooms = [room],
                AdditionalDirectionalTraversalMappings =
                [
                    new DirectionalTraversalMapping { Token = "area-bias", TraversalDirection = Direction10.South }
                ]
            };

            var country = new Country
            {
                Name = "Country",
                Areas = [area],
                AdditionalDirectionalTraversalMappings =
                [
                    new DirectionalTraversalMapping { Token = "country-bias", TraversalDirection = Direction10.North }
                ]
            };

            var planet = new Planet
            {
                Name = "Planet",
                Countries = [country],
                AdditionalDirectionalTraversalMappings =
                [
                    new DirectionalTraversalMapping { Token = "planet-bias", TraversalDirection = Direction10.East }
                ]
            };

            var project = new ProjectModel
            {
                Name = "DirectionalTraversalMappingsExport",
                Planets = [planet],
                Directionals = ["north", "south", "east", "west", "up", "down"],
                DirectionalTraversalMappings =
                [
                    new DirectionalTraversalMapping { Token = "global-bias", TraversalDirection = Direction10.NorthWest }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "DirectionalTraversalMappingsExport.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            using var planetDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindPlanetNodePath(projectFilePath, planet.Id), Encoding.UTF8));
            using var countryDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindCountryNodePath(projectFilePath, country.Id), Encoding.UTF8));
            using var areaDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindAreaNodePath(projectFilePath, area.Id), Encoding.UTF8));
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindRoomNodePath(projectFilePath, room.Id), Encoding.UTF8));
            using var objectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, scopedObject.ObjectId), Encoding.UTF8));

            AssertHasDirectionalTraversalMapping(projectDoc.RootElement, "global-bias", "NorthWest");
            AssertHasDirectionalTraversalMapping(planetDoc.RootElement, "planet-bias", "East");
            AssertHasDirectionalTraversalMapping(countryDoc.RootElement, "country-bias", "North");
            AssertHasDirectionalTraversalMapping(areaDoc.RootElement, "area-bias", "South");
            AssertHasDirectionalTraversalMapping(roomDoc.RootElement, "room-bias", "SouthEast");
            AssertHasDirectionalTraversalMapping(objectDoc.RootElement, "object-bias", "West");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_ChessDemo_ExportsAndIndexesProjectBaseObjects()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceRoot = Path.Combine(FindRepositoryRoot(), "Samples", "ChessDemo");
            var workingRoot = Path.Combine(tempRoot, "ChessDemo");
            CopyDirectory(sourceRoot, workingRoot);

            var projectFilePath = Path.Combine(workingRoot, "ChessDemo.sbe.json");
            var project = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(project);

            var cleanProjectPath = service.ExportCleanProjectV1(projectFilePath, project!);
            using var projectDoc = JsonDocument.Parse(File.ReadAllText(cleanProjectPath, Encoding.UTF8));

            var baseObjectIds = projectDoc.RootElement
                .GetProperty("baseObjectIds")
                .EnumerateArray()
                .Select(static id => id.GetGuid())
                .ToList();

            Assert.NotEmpty(baseObjectIds);

            foreach (var baseObjectId in baseObjectIds)
            {
                Assert.True(File.Exists(BuildScopeKindGameObjectNodePath(projectFilePath, baseObjectId)));
            }

            var runtimeIndexPath = Path.Combine(BuildRuntimeExportRootPath(projectFilePath), "runtime-index.html");
            Assert.True(File.Exists(runtimeIndexPath));
            var runtimeIndexHtml = File.ReadAllText(runtimeIndexPath, Encoding.UTF8);

            foreach (var baseObjectId in baseObjectIds)
            {
                Assert.Contains(baseObjectId.ToString("D").ToUpperInvariant(), runtimeIndexHtml, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildLegacyRuntimeNavigationPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, RuntimeExportFolderName, $"{baseName}.sbr.runtime.navigation.json");
    }

    private static string BuildRuntimeProjectPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, RuntimeExportFolderName, $"{baseName}.sbr.runtime.json");
    }

    private static string BuildRuntimeRoomPath(string projectFilePath, Guid roomId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Room", roomId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildRuntimeProjectBaseName(string projectFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase)
            ? baseName[..^4]
            : baseName;
    }

    private static string BuildRuntimeProcedurePath(string projectFilePath, Guid procedureId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Procedure", procedureId.ToString("N") + ".procedure.json");
    }

    private static string BuildScopeKindRoomNodePath(string projectFilePath, Guid roomId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Room", roomId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildScopeKindPlanetNodePath(string projectFilePath, Guid planetId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Planet", planetId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildScopeKindCountryNodePath(string projectFilePath, Guid countryId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Country", countryId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildScopeKindAreaNodePath(string projectFilePath, Guid areaId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Area", areaId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildScopeKindGameObjectNodePath(string projectFilePath, Guid gameObjectId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "GameObject", gameObjectId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildScopeKindBookNodePath(string projectFilePath, Guid phaseId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Book", phaseId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildRuntimeExportRootPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName);
    }

    private static string BuildCleanSharedImageFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildRuntimeExportRootPath(projectFilePath), "assets", "images", "_shared");
    }

    private static string BuildCleanSharedSoundFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildRuntimeExportRootPath(projectFilePath), "assets", "sounds", "_shared");
    }

    private static string BuildAssetsManifestPath(string projectFilePath)
    {
        return Path.Combine(BuildRuntimeExportRootPath(projectFilePath), "assets", "assets-manifest.json");
    }

    private static string BuildRuntimePresentationCueCatalogPath(string projectFilePath)
    {
        return Path.Combine(BuildRuntimeExportRootPath(projectFilePath), "assets", "PresentationCues", "presentation-effects.catalog.json");
    }

    private static string BuildExpectedPresentationCueCatalogSourcePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Config", "presentation-effects.catalog.json");
    }

    private static JsonElement LoadSingleRoomExportedObject(string projectFilePath, Guid roomId)
    {
        using var roomDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeRoomPath(projectFilePath, roomId), Encoding.UTF8));
        var objectIds = roomDoc.RootElement
            .GetProperty("gameObjectIds")
            .EnumerateArray()
            .Select(id => id.GetGuid())
            .ToList();

        var objectId = Assert.Single(objectIds);
        using var objectDoc = JsonDocument.Parse(File.ReadAllText(BuildScopeKindGameObjectNodePath(projectFilePath, objectId), Encoding.UTF8));
        return objectDoc.RootElement.Clone();
    }

    private static string WriteTinyPng(string folder, string fileName)
    {
        var fullPath = Path.Combine(folder, fileName);
        File.WriteAllBytes(fullPath, Convert.FromBase64String(TinyPngBase64));
        return fullPath;
    }

    private static string WriteTinyWav(string folder, string fileName, byte trailingByte)
    {
        var fullPath = Path.Combine(folder, fileName);
        File.WriteAllBytes(fullPath,
        [
            0x52, 0x49, 0x46, 0x46,
            0x24, 0x00, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45,
            0x66, 0x6D, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x01, 0x00,
            0x40, 0x1F, 0x00, 0x00,
            0x80, 0x3E, 0x00, 0x00,
            0x02, 0x00, 0x10, 0x00,
            0x64, 0x61, 0x74, 0x61,
            0x02, 0x00, 0x00, 0x00,
            trailingByte, 0x00
        ]);
        return fullPath;
    }

    private static void AssertRelativeCleanAssetPath(string? path)
    {
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.False(Path.IsPathRooted(path));
        Assert.StartsWith("assets/", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\\', path);
    }

    private static void AssertHasDirectionalTraversalMapping(JsonElement node, string token, string traversalDirection)
    {
        Assert.True(node.TryGetProperty("additionalDirectionalTraversalMappings", out var mappings));
        Assert.Equal(JsonValueKind.Array, mappings.ValueKind);
        Assert.Contains(mappings.EnumerateArray(), mapping =>
            string.Equals(mapping.GetProperty("token").GetString(), token, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mapping.GetProperty("traversalDirection").GetString(), traversalDirection, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        return AppContext.BaseDirectory;
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var sourceFile in Directory.EnumerateFiles(sourcePath))
        {
            var destinationFile = Path.Combine(destinationPath, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, destinationFile, overwrite: true);
        }

        foreach (var sourceDirectory in Directory.EnumerateDirectories(sourcePath))
        {
            var destinationDirectory = Path.Combine(destinationPath, Path.GetFileName(sourceDirectory));
            CopyDirectory(sourceDirectory, destinationDirectory);
        }
    }
}

