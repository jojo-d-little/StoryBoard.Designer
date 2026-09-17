using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public class JsonExportServiceRegressionProtectionTests
{
    private const string RuntimeExportFolderName = "GameRuntimeJson";

    [Fact]
    public void ExportRuntimeProjectV1_ExcludesNativeApplicationStateFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room { Name = "Room" };
            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "Regression",
                UiState = new ProjectUiState
                {
                    PlanetName = "Planet",
                    CountryName = "Country",
                    AreaName = "Area",
                    RoomId = room.Id,
                    SelectedWorkspaceTabIndex = 1
                },
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "Regression.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var cleanProjectJson = File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8);
            var legacyAggregateNavigationPath = BuildLegacyRuntimeNavigationPath(projectFilePath);
            Assert.False(File.Exists(legacyAggregateNavigationPath));

            var areaScopePath = BuildScopeKindAreaNodePath(projectFilePath, area.Id);
            using var areaDoc = JsonDocument.Parse(File.ReadAllText(areaScopePath, Encoding.UTF8));

            Assert.DoesNotContain("\"uiState\"", cleanProjectJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selectedWorkspaceTabIndex", cleanProjectJson, StringComparison.OrdinalIgnoreCase);
            Assert.False(areaDoc.RootElement.TryGetProperty("uiState", out _));
            Assert.DoesNotContain("selectedWorkspaceTabIndex", areaDoc.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public void SaveProjectModel_DoesNotPersistTransientPreviewVisibilityState()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Room",
                Images = new List<RoomImageEntry>
                {
                    new()
                    {
                        Slot = RoomImageSlot.Default,
                        Image = new RoomImageVariant { FullImagePath = "default.png" }
                    },
                    new()
                    {
                        Slot = RoomImageSlot.North,
                        Image = new RoomImageVariant { FullImagePath = "north.png" }
                    }
                }
            };

            var editorVm = new RoomDesignerTabViewModel(room, 800, 600);
            Assert.NotNull(editorVm.NorthSlot);
            editorVm.NorthSlot!.IsPreviewVisible = false;

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "Regression",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "Regression.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomJsonPath = BuildRoomFilePath(projectFilePath, room.Id);
            var roomJson = File.ReadAllText(roomJsonPath, Encoding.UTF8);

            Assert.DoesNotContain("isPreviewVisible", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("previewVisible", roomJson, StringComparison.OrdinalIgnoreCase);
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
    public void SaveProjectModel_RoundTripsRoomDisplayMode()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Room",
                RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay,
                Images = new List<RoomImageEntry>
                {
                    new()
                    {
                        Slot = RoomImageSlot.Default,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(RoomImageSlot.Default),
                        Image = new RoomImageVariant()
                    },
                    new()
                    {
                        Slot = RoomImageSlot.North,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(RoomImageSlot.North),
                        Image = new RoomImageVariant()
                    }
                }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "Regression",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "Regression.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedRoom = loaded?.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();

            Assert.NotNull(loadedRoom);
            Assert.Equal(RuntimeRoomImageDisplayMode.Overlay, loadedRoom!.RoomDisplayMode);
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
    public void SaveProjectModel_DoesNotPersistDirectionalUseDefaultOrDisplayMode()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Room",
                RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay,
                Images = new List<RoomImageEntry>
                {
                    new()
                    {
                        Slot = RoomImageSlot.Default,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(RoomImageSlot.Default),
                        Image = new RoomImageVariant { FullImagePath = "default.png" }
                    },
                    new()
                    {
                        Slot = RoomImageSlot.Down,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(RoomImageSlot.Down),
                        Image = new RoomImageVariant { FullImagePath = "down.png" }
                    }
                }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "Regression",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "Regression.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomJsonPath = BuildRoomFilePath(projectFilePath, room.Id);
            var roomJson = File.ReadAllText(roomJsonPath, Encoding.UTF8);

            Assert.DoesNotContain("\"useDefaultImage\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"displayMode\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"roomDisplayMode\"", roomJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"overlayRenderOrder\"", roomJson, StringComparison.OrdinalIgnoreCase);
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
    public void SaveProjectModel_RoundTripsScopedAuthoredGameObjects()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room { Name = "Room" };
            var areaObject = new GameObject { Name = "Area Statue" };
            var countryObject = new GameObject { Name = "Country Banner" };
            var planetObject = new GameObject { Name = "Planet Beacon" };

            var area = new Area
            {
                Name = "Area",
                Rooms = new List<Room> { room },
                GameObjects = new List<GameObject> { areaObject }
            };

            var country = new Country
            {
                Name = "Country",
                Areas = new List<Area> { area },
                GameObjects = new List<GameObject> { countryObject }
            };

            var planet = new Planet
            {
                Name = "Planet",
                Countries = new List<Country> { country },
                GameObjects = new List<GameObject> { planetObject }
            };

            var project = new ProjectModel
            {
                Name = "ScopedObjectsRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "ScopedObjectsRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedPlanet = Assert.Single(loaded!.Planets);
            var loadedCountry = Assert.Single(loadedPlanet.Countries);
            var loadedArea = Assert.Single(loadedCountry.Areas);

            Assert.Equal("Planet Beacon", Assert.Single(loadedPlanet.GameObjects).Name);
            Assert.Equal("Country Banner", Assert.Single(loadedCountry.GameObjects).Name);
            Assert.Equal("Area Statue", Assert.Single(loadedArea.GameObjects).Name);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildRuntimeProjectPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, RuntimeExportFolderName, $"{baseName}.sbr.runtime.json");
    }

    private static string BuildLegacyRuntimeNavigationPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, RuntimeExportFolderName, $"{baseName}.sbr.runtime.navigation.json");
    }

    private static string BuildScopeKindAreaNodePath(string projectFilePath, Guid areaId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Area", areaId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildRuntimeProjectBaseName(string projectFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase)
            ? baseName[..^4]
            : baseName;
    }

    private static string BuildRoomFilePath(string projectFilePath, Guid roomId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Room", $"{roomId:N}.room.json");
    }
}

