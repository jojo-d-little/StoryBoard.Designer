using System.Text.Json;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceLightingPersistenceTests
{
    [Fact]
    public void SaveProjectModel_RoundTripsLightingAcrossAuthoringScopes()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomObject = new GameObject
            {
                Name = "Lantern",
                PointLight = new PointLight
                {
                    OffsetXPx = 12.5,
                    OffsetYPx = 24.25,
                    RadiusPx = 180,
                    DirectionDeg = 35,
                    ConeAngleDeg = 90,
                    MotionMode = "Sway",
                    Phase = 0.25,
                    Color = "#FFE0A0",
                    OuterColor = "#FF8A3D",
                    GradientExponent = 2.2,
                    IntensityScale = 0.75,
                    LightHeightCells = 1.5,
                    SwayAmountPx = 3,
                    SwayHz = 0.8,
                    SwayDirectionDeg = 15,
                    FlickerAmount = 0.2,
                    FlickerHz = 4,
                    FlickerStyle = "Candle"
                },
                LightOcclusion = new LightOcclusion { Strength = 0.65 },
                IsPointLightEnabledDefaultValue = false
            };

            var room = new Room
            {
                Name = "Lit Room",
                AmbientLighting = new RoomAmbientLighting
                {
                    Ambient = 0.4,
                    AmbientColor = "#203040"
                },
                GameObjects = [roomObject]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var page = new PhaseNode
            {
                Tier = PhaseTier.Page,
                PhaseKey = "book.chapter.page",
                DisplayName = "Page",
                PhaseAmbientOverlay = new PhaseAmbientOverlay
                {
                    AmbientScale = 0.6,
                    Color = "#304050",
                    ColorMode = PhaseAmbientColorMode.Blend,
                    ColorBlend = 0
                }
            };
            var chapter = new PhaseNode
            {
                Tier = PhaseTier.Chapter,
                PhaseKey = "book.chapter",
                DisplayName = "Chapter"
            };
            chapter.AddChildScope(page);
            var book = new PhaseNode
            {
                Tier = PhaseTier.Book,
                PhaseKey = "book",
                DisplayName = "Book"
            };
            book.AddChildScope(chapter);

            var project = new ProjectModel
            {
                Name = "LightingRoundTrip",
                PointLightDefaults = new PointLightDefaults
                {
                    RadiusPx = 220,
                    IntensityScale = 0.9,
                    Color = "#FFFFFF",
                    OuterColor = "#A0B0C0",
                    GradientExponent = 1.8,
                    LightHeightCells = 2,
                    SwayAmountPx = 1.5,
                    SwayHz = 0.4,
                    SwayDirectionDeg = 270,
                    FlickerAmount = 0.1,
                    FlickerHz = 3,
                    FlickerStyle = "Soft"
                },
                Planets = [planet],
                PhaseBooks = [book]
            };

            var projectFilePath = Path.Combine(tempRoot, "LightingRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using (var rootDocument = JsonDocument.Parse(File.ReadAllText(projectFilePath)))
            {
                var defaults = rootDocument.RootElement.GetProperty("pointLightDefaults");
                Assert.Equal(220, defaults.GetProperty("radiusPx").GetDouble());
                Assert.Equal(0.9, defaults.GetProperty("intensityScale").GetDouble());
            }

            using (var roomDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "Room", $"{room.Id:N}.room.json"))))
            {
                var ambient = roomDocument.RootElement.GetProperty("ambientLighting");
                Assert.Equal(0.4, ambient.GetProperty("ambient").GetDouble());
                Assert.Equal("#203040", ambient.GetProperty("ambientColor").GetString());
            }

            using (var objectDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "GameObject", $"{roomObject.ObjectId:N}.object.json"))))
            {
                var pointLight = objectDocument.RootElement.GetProperty("pointLight");
                Assert.Equal(12.5, pointLight.GetProperty("offsetXPx").GetDouble());
                Assert.Equal(24.25, pointLight.GetProperty("offsetYPx").GetDouble());
                Assert.Equal(180, pointLight.GetProperty("radiusPx").GetDouble());
                Assert.Equal(0.65, objectDocument.RootElement.GetProperty("lightOcclusion").GetProperty("strength").GetDouble());
                Assert.False(objectDocument.RootElement.GetProperty("isPointLightEnabledDefaultValue").GetBoolean());
            }

            using (var phaseDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "Book", $"{page.Id:N}.phase.json"))))
            {
                var overlay = phaseDocument.RootElement.GetProperty("phaseAmbientOverlay");
                Assert.Equal(0.6, overlay.GetProperty("ambientScale").GetDouble());
                Assert.Equal("Blend", overlay.GetProperty("colorMode").GetString());
                Assert.Equal(0, overlay.GetProperty("colorBlend").GetDouble());
            }

            var loaded = service.TryLoadProjectModel(projectFilePath)!;
            var loadedObject = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().GameObjects.Single();
            var loadedPage = loaded.PhaseBooks.Single().Children.Single().Children.Single();

            Assert.Equal(220, loaded.PointLightDefaults!.RadiusPx);
            Assert.Equal(0.9, loaded.PointLightDefaults.IntensityScale);
            Assert.Equal(0.4, room.AmbientLighting!.Ambient);
            Assert.Equal(0.4, loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().AmbientLighting!.Ambient);
            Assert.Equal("#203040", loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().AmbientLighting!.AmbientColor);
            Assert.Equal(12.5, loadedObject.PointLight!.OffsetXPx);
            Assert.Equal(24.25, loadedObject.PointLight.OffsetYPx);
            Assert.Equal(0.65, loadedObject.LightOcclusion!.Strength);
            Assert.False(loadedObject.IsPointLightEnabledDefaultValue);
            Assert.Equal(0.6, loadedPage.PhaseAmbientOverlay!.AmbientScale);
            Assert.Equal(PhaseAmbientColorMode.Blend, loadedPage.PhaseAmbientOverlay.ColorMode);
            Assert.Equal(0, loadedPage.PhaseAmbientOverlay.ColorBlend);
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
    public void SaveProjectModel_OmitsUnsetLightingFields_AndLoadPreservesLegacyDefaults()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomObject = new GameObject { Name = "Chair" };
            var room = new Room { Name = "Room", GameObjects = [roomObject] };
            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "LightingLegacyDefaults",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "LightingLegacyDefaults.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using (var rootDocument = JsonDocument.Parse(File.ReadAllText(projectFilePath)))
            {
                Assert.False(rootDocument.RootElement.TryGetProperty("pointLightDefaults", out _));
            }

            using (var roomDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "Room", $"{room.Id:N}.room.json"))))
            {
                Assert.False(roomDocument.RootElement.TryGetProperty("ambientLighting", out _));
            }

            using (var objectDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "GameObject", $"{roomObject.ObjectId:N}.object.json"))))
            {
                Assert.False(objectDocument.RootElement.TryGetProperty("pointLight", out _));
                Assert.False(objectDocument.RootElement.TryGetProperty("lightOcclusion", out _));
            }

            var loaded = service.TryLoadProjectModel(projectFilePath)!;
            var loadedObject = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().GameObjects.Single();

            Assert.Null(loaded.PointLightDefaults);
            Assert.Null(loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().AmbientLighting);
            Assert.Null(loadedObject.PointLight);
            Assert.Null(loadedObject.LightOcclusion);
            Assert.True(loadedObject.IsPointLightEnabledDefaultValue);
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
    public void SaveProjectModel_OverridePhaseAmbientOverlay_DoesNotRegainIgnoredColorBlendAfterReload()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var page = new PhaseNode
            {
                Tier = PhaseTier.Page,
                PhaseKey = "book.chapter.page",
                DisplayName = "Page",
                PhaseAmbientOverlay = new PhaseAmbientOverlay
                {
                    Color = "#304050",
                    ColorMode = PhaseAmbientColorMode.Override,
                    ColorBlend = null
                }
            };
            var chapter = new PhaseNode { Tier = PhaseTier.Chapter, PhaseKey = "book.chapter", DisplayName = "Chapter" };
            chapter.AddChildScope(page);
            var book = new PhaseNode { Tier = PhaseTier.Book, PhaseKey = "book", DisplayName = "Book" };
            book.AddChildScope(chapter);
            var project = new ProjectModel { Name = "OverrideAmbient", PhaseBooks = [book] };
            var projectFilePath = Path.Combine(tempRoot, "OverrideAmbient.sbe.json");

            service.SaveProjectModel(projectFilePath, project);

            var phasePath = Path.Combine(tempRoot, "Book", $"{page.Id:N}.phase.json");
            using (var document = JsonDocument.Parse(File.ReadAllText(phasePath)))
            {
                Assert.False(document.RootElement.GetProperty("phaseAmbientOverlay").TryGetProperty("colorBlend", out _));
            }

            var loaded = service.TryLoadProjectModel(projectFilePath)!;
            var loadedPage = loaded.PhaseBooks.Single().Children.Single().Children.Single();
            Assert.Equal(PhaseAmbientColorMode.Override, loadedPage.PhaseAmbientOverlay!.ColorMode);
            Assert.Null(loadedPage.PhaseAmbientOverlay.ColorBlend);
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
    public void ExportCleanProjectV1_WritesRuntimeLightingFields_WithLocalObjectOffsets()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomObject = new GameObject
            {
                Name = "Torch",
                PositionX = 900,
                PositionY = 700,
                PointLight = new PointLight
                {
                    OffsetXPx = 8,
                    OffsetYPx = 12,
                    RadiusPx = 160,
                    Color = "#FFD080"
                },
                LightOcclusion = new LightOcclusion { Strength = 0.25 },
                IsPointLightEnabledDefaultValue = false
            };
            var room = new Room
            {
                Name = "Runtime Room",
                AmbientLighting = new RoomAmbientLighting { Ambient = 0.55, AmbientColor = "#101820" },
                GameObjects = [roomObject]
            };
            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var page = new PhaseNode
            {
                Tier = PhaseTier.Page,
                PhaseKey = "book.chapter.page",
                DisplayName = "Page",
                PhaseAmbientOverlay = new PhaseAmbientOverlay
                {
                    AmbientScale = 1.2,
                    Color = "#506070",
                    ColorMode = PhaseAmbientColorMode.Override,
                    ColorBlend = 0.2
                }
            };
            var chapter = new PhaseNode { Tier = PhaseTier.Chapter, PhaseKey = "book.chapter", DisplayName = "Chapter" };
            chapter.AddChildScope(page);
            var book = new PhaseNode { Tier = PhaseTier.Book, PhaseKey = "book", DisplayName = "Book" };
            book.AddChildScope(chapter);
            var project = new ProjectModel
            {
                Name = "RuntimeLighting",
                PointLightDefaults = new PointLightDefaults { RadiusPx = 200, IntensityScale = 0.8 },
                Planets = [planet],
                PhaseBooks = [book]
            };

            var projectFilePath = Path.Combine(tempRoot, "RuntimeLighting.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            var runtimeRoot = Path.Combine(tempRoot, "GameRuntimeJson");
            using (var projectDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtimeRoot, "RuntimeLighting.sbr.runtime.json"))))
            {
                var defaults = projectDocument.RootElement.GetProperty("pointLightDefaults");
                Assert.Equal(200, defaults.GetProperty("radiusPx").GetDouble());
                Assert.Equal(0.8, defaults.GetProperty("intensityScale").GetDouble());
            }

            using (var roomDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtimeRoot, "Room", $"{room.Id:D}".ToUpperInvariant() + ".runtime.json"))))
            {
                var ambient = roomDocument.RootElement.GetProperty("ambientLighting");
                Assert.Equal(0.55, ambient.GetProperty("ambient").GetDouble());
            }

            var objectPath = Path.Combine(runtimeRoot, "GameObject", $"{roomObject.ObjectId:D}".ToUpperInvariant() + ".runtime.json");
            using var objectDocument = JsonDocument.Parse(File.ReadAllText(objectPath));
            var pointLight = objectDocument.RootElement.GetProperty("pointLight");
            Assert.Equal(8, pointLight.GetProperty("offsetXPx").GetDouble());
            Assert.Equal(12, pointLight.GetProperty("offsetYPx").GetDouble());
            Assert.False(pointLight.TryGetProperty("x", out _));
            Assert.False(pointLight.TryGetProperty("y", out _));
            Assert.Equal(0.25, objectDocument.RootElement.GetProperty("lightOcclusion").GetProperty("strength").GetDouble());
            Assert.False(objectDocument.RootElement.GetProperty("isPointLightEnabledDefaultValue").GetBoolean());

            var phasePath = Path.Combine(runtimeRoot, "Book", $"{page.Id:D}".ToUpperInvariant() + ".runtime.json");
            using var phaseDocument = JsonDocument.Parse(File.ReadAllText(phasePath));
            var overlay = phaseDocument.RootElement.GetProperty("phaseAmbientOverlay");
            Assert.Equal(1.2, overlay.GetProperty("ambientScale").GetDouble());
            Assert.Equal("Override", overlay.GetProperty("colorMode").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
