using System.Text;
using System.Text.Json;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public class JsonExportServiceScopeKindPersistenceTests
{
    [Fact]
    public void SaveAndLoadProjectModel_PersistsExplicitScopeKinds_ForTemplateAndRuntimeCatalogs()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectTemplate = new GameObject
            {
                Name = "Template Crate",
                ProducerNotes = "template-crate"
            };

            var roomTemplateObject = new GameObject
            {
                Name = "Template Room Object",
                ProducerNotes = "template-room-object"
            };

            var roomTemplate = new Room
            {
                Name = "Template Room",
                GameObjects = [roomTemplateObject]
            };

            var liveRoomObject = new GameObject
            {
                Name = "Live Room Object",
                ProducerNotes = "live-room-object"
            };

            var liveRoom = new Room
            {
                Name = "Live Room",
                GameObjects = [liveRoomObject]
            };

            var area = new Area { Name = "Area A", Rooms = [liveRoom] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };

            var project = new ProjectModel
            {
                Name = "ScopeKindPersistence",
                ObjectTemplates = [objectTemplate],
                RoomTemplates = [roomTemplate],
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "ScopeKindPersistence.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var globalsPath = BuildProjectGlobalsFilePath(projectFilePath);
            using (var globalsDoc = JsonDocument.Parse(File.ReadAllText(globalsPath, Encoding.UTF8)))
            {
                var objectTemplateIds = globalsDoc.RootElement.GetProperty("objectTemplateIds").EnumerateArray().Select(static id => id.GetGuid()).ToList();
                var roomTemplateIds = globalsDoc.RootElement.GetProperty("roomTemplateIds").EnumerateArray().Select(static id => id.GetGuid()).ToList();

                Assert.Contains(objectTemplate.ObjectId, objectTemplateIds);
                Assert.Contains(roomTemplate.Id, roomTemplateIds);
                Assert.Equal(0, globalsDoc.RootElement.GetProperty("objectTemplates").GetArrayLength());
                Assert.Equal(0, globalsDoc.RootElement.GetProperty("roomTemplates").GetArrayLength());
            }

            var templateObjectFilePath = BuildTemplateObjectFilePath(projectFilePath, objectTemplate.ObjectId);
            using (var templateDoc = JsonDocument.Parse(File.ReadAllText(templateObjectFilePath, Encoding.UTF8)))
            {
                Assert.Equal("templates", templateDoc.RootElement.GetProperty("scopeKind").GetString(), ignoreCase: true);
            }

            var roomTemplateObjectFilePath = BuildTemplateObjectFilePath(projectFilePath, roomTemplateObject.ObjectId);
            using (var templateRoomObjectDoc = JsonDocument.Parse(File.ReadAllText(roomTemplateObjectFilePath, Encoding.UTF8)))
            {
                Assert.Equal("templates", templateRoomObjectDoc.RootElement.GetProperty("scopeKind").GetString(), ignoreCase: true);
            }

            var liveRoomObjectFilePath = BuildRuntimeObjectFilePath(projectFilePath, liveRoomObject.ObjectId);
            Assert.True(File.Exists(liveRoomObjectFilePath));

            var roomTemplateFilePath = BuildRoomTemplateFilePath(projectFilePath, roomTemplate.Id);
            using (var roomTemplateDoc = JsonDocument.Parse(File.ReadAllText(roomTemplateFilePath, Encoding.UTF8)))
            {
                Assert.Equal("roomTemplates", roomTemplateDoc.RootElement.GetProperty("scopeKind").GetString(), ignoreCase: true);
                var gameObjectIds = roomTemplateDoc.RootElement.GetProperty("gameObjectIds").EnumerateArray().Select(static id => id.GetGuid()).ToList();
                Assert.Contains(roomTemplateObject.ObjectId, gameObjectIds);
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedTemplate = Assert.Single(loaded!.ObjectTemplates);
            Assert.Equal(ScopeNodeKind.Templates, loadedTemplate.DesignerPersistenceScopeKind);

            var loadedRoomTemplate = Assert.Single(loaded.RoomTemplates);
            Assert.Equal(ScopeNodeKind.RoomTemplates, loadedRoomTemplate.DesignerPersistenceScopeKind);
            var loadedRoomTemplateObject = Assert.Single(loadedRoomTemplate.GameObjects);
            Assert.Equal(ScopeNodeKind.Templates, loadedRoomTemplateObject.DesignerPersistenceScopeKind);

            var loadedLiveRoom = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var loadedLiveRoomObject = Assert.Single(loadedLiveRoom.GameObjects);
            Assert.Equal(ScopeNodeKind.GameObject, loadedLiveRoomObject.DesignerPersistenceScopeKind);
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
    public void SaveProjectModel_WritesAuthoringIndex_UsingScopeKindDrivenTemplateAndRuntimePaths()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectTemplate = new GameObject { Name = "Template Crate" };
            var roomTemplate = new Room
            {
                Name = "Template Room",
                GameObjects = [new GameObject { Name = "Template Room Object" }]
            };

            var liveRoom = new Room
            {
                Name = "Live Room",
                GameObjects = [new GameObject { Name = "Live Room Object" }]
            };

            var area = new Area { Name = "Area A", Rooms = [liveRoom] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };

            var project = new ProjectModel
            {
                Name = "ScopeKindIndex",
                ObjectTemplates = [objectTemplate],
                RoomTemplates = [roomTemplate],
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "ScopeKindIndex.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var indexPath = Path.Combine(Path.GetDirectoryName(projectFilePath) ?? string.Empty, "authoring-index.html");
            Assert.True(File.Exists(indexPath));

            var html = File.ReadAllText(indexPath, Encoding.UTF8);

            Assert.Contains("<td>Global</td>", html, StringComparison.Ordinal);
            Assert.Contains("project.objectTemplates.Template Crate", html, StringComparison.Ordinal);
            Assert.Contains("<td>Templates</td>", html, StringComparison.Ordinal);
            Assert.Contains("<td>RoomTemplates</td>", html, StringComparison.Ordinal);
            Assert.Contains("project.roomTemplates.Template Room.templateObjects.Template Room Object", html, StringComparison.Ordinal);
            Assert.DoesNotContain("project.roomTemplates.Template Room.gameObjects.", html, StringComparison.Ordinal);
            Assert.Contains("project.planets.Planet A.countries.Country A.areas.Area A.rooms.Live Room.gameObjects.Live Room Object", html, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildProjectGlobalsFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.globals.json");
    }

    private static string BuildTemplateObjectFilePath(string projectFilePath, Guid objectId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Templates", $"{objectId:N}.object.json");
    }

    private static string BuildRuntimeObjectFilePath(string projectFilePath, Guid objectId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "GameObject", $"{objectId:N}.object.json");
    }

    private static string BuildRoomTemplateFilePath(string projectFilePath, Guid roomTemplateId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "RoomTemplate", $"{roomTemplateId:N}.roomtemplate.json");
    }
}
