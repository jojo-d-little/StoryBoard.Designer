using System.Text.Json;
using System.Text.Json.Nodes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceObjectPlacementPersistenceTests
{
    [Fact]
    public void SaveProjectModel_RoundTripsRoomChildObjectPlacementFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Lamp",
                PositionX = 123.5,
                PositionY = 66.25,
                IncludeInPreview = false
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = new List<GameObject> { roomChild }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "PlacementRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "PlacementRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Equal(123.5, loadedObject.PositionX);
            Assert.Equal(66.25, loadedObject.PositionY);
            Assert.False(loadedObject.IncludeInPreview);
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
    public void TryLoadProjectModel_DefaultsPlacementFields_WhenMissingFromLegacyPayload()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Chair",
                PositionX = 500,
                PositionY = 300,
                IncludeInPreview = false
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = new List<GameObject> { roomChild }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "PlacementLegacyDefaults",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "PlacementLegacyDefaults.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var objectJsonPath = BuildAuthoringGameObjectFilePath(projectFilePath, roomChild.ObjectId);
            var objectNode = JsonNode.Parse(File.ReadAllText(objectJsonPath))!;
            RemovePropertyRecursively(objectNode, "positionX");
            RemovePropertyRecursively(objectNode, "positionY");
            RemovePropertyRecursively(objectNode, "includeInPreview");
            File.WriteAllText(objectJsonPath, objectNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Equal(0, loadedObject.PositionX);
            Assert.Equal(0, loadedObject.PositionY);
            Assert.True(loadedObject.IncludeInPreview);
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
    public void TryLoadProjectModel_DoesNotHydrateAuthoredRenderOrderFromLegacyRenderZOrder()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Table"
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = new List<GameObject> { roomChild }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "LegacyRenderOnly",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LegacyRenderOnly.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var objectJsonPath = BuildAuthoringGameObjectFilePath(projectFilePath, roomChild.ObjectId);
            var firstObject = JsonNode.Parse(File.ReadAllText(objectJsonPath))?.AsObject()
                ?? throw new InvalidOperationException("Missing object payload.");
            firstObject["renderZOrder"] = 2222;
            firstObject.Remove("authoredRenderOrder");
            File.WriteAllText(objectJsonPath, firstObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Equal(2222, loadedObject.RenderZOrder);
            Assert.Equal(0, loadedObject.AuthoredRenderOrder);
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
    public void SaveProjectModel_RecomputesDerivedOccupancy_WhenCacheFieldsArePresent()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Cabinet",
                PositionX = 45,
                PositionY = 80,
                FootprintWidthCells = 2,
                FootprintHeightCells = 1,
                FootprintOrientation = "N",
                OccupiedCellIds = ["Z.Z"],
                OccupancyDerivationSourceEcho = "stale"
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = new List<GameObject> { roomChild }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "OccupancySaveRecompute",
                RoomDesignerGridCellSize = 40,
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "OccupancySaveRecompute.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var objectJsonPath = BuildAuthoringGameObjectFilePath(projectFilePath, roomChild.ObjectId);
            using var objectDoc = JsonDocument.Parse(File.ReadAllText(objectJsonPath));
            var savedObject = objectDoc.RootElement;
            var appearance = savedObject.GetProperty("appearance");

            var occupiedCells = appearance.GetProperty("occupiedCellIds").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Equal(new[] { "B.C", "C.C" }, occupiedCells);
            Assert.Equal(
                "x=45;y=80;anchor=TopLeft;footprint=2x1;orientation=N;cell=40",
                appearance.GetProperty("occupancyDerivationSourceEcho").GetString());
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
    public void TryLoadProjectModel_RecomputesDerivedOccupancy_WhenPersistedCacheDrifts()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Desk",
                PositionX = 45,
                PositionY = 80,
                FootprintWidthCells = 2,
                FootprintHeightCells = 1,
                FootprintOrientation = "N",
                OccupiedCellIds = ["Z.Z"],
                OccupancyDerivationSourceEcho = "stale"
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = new List<GameObject> { roomChild }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "OccupancyLoadRecompute",
                RoomDesignerGridCellSize = 40,
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "OccupancyLoadRecompute.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var objectJsonPath = BuildAuthoringGameObjectFilePath(projectFilePath, roomChild.ObjectId);
            var firstObject = JsonNode.Parse(File.ReadAllText(objectJsonPath))?.AsObject()
                ?? throw new InvalidOperationException("Missing object payload.");
            var appearanceNode = firstObject["appearance"]?.AsObject() ?? throw new InvalidOperationException("Missing appearance object.");
            appearanceNode["occupiedCellIds"] = new JsonArray("Q.Q");
            appearanceNode["occupancyDerivationSourceEcho"] = "tampered";
            File.WriteAllText(objectJsonPath, firstObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Equal(new[] { "B.C", "C.C" }, loadedObject.OccupiedCellIds);
            Assert.Equal("x=45;y=80;anchor=TopLeft;footprint=2x1;orientation=N;cell=40", loadedObject.OccupancyDerivationSourceEcho);
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
    public void SaveProjectModel_RoundTripsObjectImageVariantsAndChooserScript()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Door",
                ImageVariantChooserScript = "if {isOpen} == true then \"open\" else \"closed\"",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "closed",
                        FullImagePath = "assets/door-closed.png",
                        IsDefault = true
                    },
                    new ObjectImageVariant
                    {
                        VariantName = "open",
                        FullImagePath = "assets/door-open.png"
                    }
                ]
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [roomChild]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "ImageVariantRoundTrip",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "ImageVariantRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Equal("if {isOpen} == true then \"open\" else \"closed\"", loadedObject.ImageVariantChooserScript);
            Assert.Equal(2, loadedObject.ImageVariants.Count);
            Assert.Equal("closed", loadedObject.ImageVariants[0].VariantName);
            Assert.Equal("assets/door-closed.png", loadedObject.ImageVariants[0].FullImagePath);
            Assert.True(loadedObject.ImageVariants[0].IsDefault);
            Assert.Equal("open", loadedObject.ImageVariants[1].VariantName);
            Assert.Equal("assets/door-open.png", loadedObject.ImageVariants[1].FullImagePath);
            Assert.False(loadedObject.ImageVariants[1].IsDefault);
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
    public void SaveProjectModel_RoundTripsObjectImageRotationDegrees()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Sign",
                ImageRotationDegrees = 137.5,
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = "assets/sign.png",
                        IsDefault = true
                    }
                ]
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [roomChild]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "ImageRotationRoundTrip",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "ImageRotationRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Equal(137.5, loadedObject.ImageRotationDegrees);
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
    public void SaveProjectModel_RoundTripsObjectMovementRestrictions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Crate",
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions
                {
                    MultiLegMaxTotalDistanceCells = 5,
                    SubsequentUnstacked = new ObjectMovementRestrictionCategory
                    {
                        E = new ObjectMovementRestrictionRule
                        {
                            MaxDistance = 2,
                            AllowJumpOver = false
                        }
                    }
                }
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [roomChild]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "MovementRestrictionsRoundTrip",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "MovementRestrictionsRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.NotNull(loadedObject.MovementRestrictions);
            Assert.Equal(5, loadedObject.MovementRestrictions!.MultiLegMaxTotalDistanceCells);
            Assert.NotNull(loadedObject.MovementRestrictions!.SubsequentUnstacked);
            Assert.Equal(2, loadedObject.MovementRestrictions.SubsequentUnstacked.E.MaxDistance);
            Assert.False(loadedObject.MovementRestrictions.SubsequentUnstacked.E.AllowJumpOver);
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
    public void SaveProjectModel_DropsMovementRestrictions_WhenNoDirectionalRulesConfigured()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Barrel",
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions()
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [roomChild]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "MovementRestrictionsFlagsRoundTrip",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "MovementRestrictionsFlagsRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Null(loadedObject.MovementRestrictions);
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
    public void TryLoadProjectModel_DefaultsMissingRoomCanvasDimensions_FromProjectDefaults_AndWritesBackOnSave()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "LegacyRoom"
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "LegacyRoomDimensions",
                RoomImageCanvasWidth = 960,
                RoomImageCanvasHeight = 640,
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "LegacyRoomDimensions.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = BuildRoomFilePath(projectFilePath, room.Id);
            var roomNode = JsonNode.Parse(File.ReadAllText(roomFilePath))!;
            RemovePropertyRecursively(roomNode, "roomImageCanvasWidth");
            RemovePropertyRecursively(roomNode, "roomImageCanvasHeight");
            File.WriteAllText(roomFilePath, roomNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedRoom = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single();

            Assert.Equal(960, loadedRoom.RoomImageCanvasWidth);
            Assert.Equal(640, loadedRoom.RoomImageCanvasHeight);

            service.SaveProjectModel(projectFilePath, loaded);

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath));
            Assert.Equal(960, roomDoc.RootElement.GetProperty("roomImageCanvasWidth").GetInt32());
            Assert.Equal(640, roomDoc.RootElement.GetProperty("roomImageCanvasHeight").GetInt32());
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
    public void TryLoadProjectModel_HydratesLinkedInstanceMovementRestrictions_FromDefinition()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var definition = new GameObject
            {
                Name = "BaseCrate",
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions
                {
                    SubsequentStacked = new ObjectMovementRestrictionCategory
                    {
                        NW = new ObjectMovementRestrictionRule
                        {
                            MaxDistance = 1,
                            AllowJumpOver = false
                        }
                    }
                }
            };

            var instance = new GameObject
            {
                Name = "BaseCrate Instance",
                LinkedBaseObjectId = definition.ObjectId,
                LinkActionsToBaseObject = true,
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions
                {
                    SubsequentStacked = new ObjectMovementRestrictionCategory
                    {
                        NW = new ObjectMovementRestrictionRule
                        {
                            MaxDistance = 1,
                            AllowJumpOver = false
                        }
                    }
                }
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [instance]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "LinkedMovementHydration",
                ObjectTemplates = [definition],
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedMovementHydration.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedInstance = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.NotNull(loadedInstance.MovementRestrictions);
            Assert.Equal(1, loadedInstance.MovementRestrictions!.SubsequentStacked.NW.MaxDistance);
            Assert.False(loadedInstance.MovementRestrictions.SubsequentStacked.NW.AllowJumpOver);
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
    public void SaveProjectModel_RoundTripsMovementRestrictions_WhenLinkActionsFlagSetWithoutLinkedBaseObjectId()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "FlagOnlyLinked",
                LinkActionsToBaseObject = true,
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions
                {
                    MultiLegMaxTotalDistanceCells = 5,
                    FirstStacked = new ObjectMovementRestrictionCategory
                    {
                        S = new ObjectMovementRestrictionRule
                        {
                            MaxDistance = 2,
                            AllowJumpOver = null
                        }
                    }
                }
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [roomChild]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "FlagOnlyLinkedRoundTrip",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "FlagOnlyLinkedRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.NotNull(loadedObject.MovementRestrictions);
            Assert.Equal(5, loadedObject.MovementRestrictions!.MultiLegMaxTotalDistanceCells);
            Assert.Equal(2, loadedObject.MovementRestrictions!.FirstStacked.S.MaxDistance);
            Assert.Null(loadedObject.MovementRestrictions.FirstStacked.S.AllowJumpOver);
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
    public void SaveProjectModel_PreservesMovementRestrictions_WhenOnlyMultiLegTotalDistanceConfigured()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Knight",
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions
                {
                    MultiLegMaxTotalDistanceCells = 5
                }
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [roomChild]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "MovementRestrictionsTotalOnlyRoundTrip",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "MovementRestrictionsTotalOnlyRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.NotNull(loadedObject.MovementRestrictions);
            Assert.Equal(5, loadedObject.MovementRestrictions!.MultiLegMaxTotalDistanceCells);
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
    public void SaveProjectModel_NormalizesImageVariants_ToSingleDefaultAndCaseInsensitiveUniqueNames()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomChild = new GameObject
            {
                Name = "Door",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = " Closed ",
                        FullImagePath = "assets/door-closed.png",
                        IsDefault = true
                    },
                    new ObjectImageVariant
                    {
                        VariantName = "closed",
                        FullImagePath = "assets/duplicate-should-drop.png"
                    },
                    new ObjectImageVariant
                    {
                        VariantName = "Open",
                        FullImagePath = "assets/door-open.png",
                        IsDefault = true
                    }
                ]
            };

            var room = new Room { Name = "Room", GameObjects = [roomChild] };
            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel { Name = "NormalizeVariants", Planets = [planet] };

            var projectFilePath = Path.Combine(tempRoot, "NormalizeVariants.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedObject = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .GameObjects.Single();

            Assert.Equal(2, loadedObject.ImageVariants.Count);
            Assert.Equal("Closed", loadedObject.ImageVariants[0].VariantName);
            Assert.Equal("assets/door-closed.png", loadedObject.ImageVariants[0].FullImagePath);
            Assert.True(loadedObject.ImageVariants[0].IsDefault);
            Assert.Equal("Open", loadedObject.ImageVariants[1].VariantName);
            Assert.False(loadedObject.ImageVariants[1].IsDefault);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void RemovePropertyRecursively(JsonNode? node, string propertyName)
    {
        if (node is JsonObject obj)
        {
            obj.Remove(propertyName);
            foreach (var child in obj)
            {
                RemovePropertyRecursively(child.Value, propertyName);
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                RemovePropertyRecursively(child, propertyName);
            }
        }
    }

    private static string BuildRoomFilePath(string projectFilePath, Guid roomId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Room", $"{roomId:N}.room.json");
    }

    private static string BuildAuthoringGameObjectFilePath(string projectFilePath, Guid objectId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "GameObject", $"{objectId:N}.object.json");
    }
}

