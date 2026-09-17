using System.Text.Json;
using System.Text.Json.Nodes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceHideEmptyConfigurationPersistenceTests
{
    [Fact]
    public void SaveProjectModel_RoundTripsHideEmptyConfiguration_AcrossSupportedNodes()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var nestedObject = new GameObject { Name = "Nested", HideEmptyConfiguration = true };
            var roomObject = new GameObject
            {
                Name = "RoomObject",
                HideEmptyConfiguration = true,
                ContainedObjects = [nestedObject]
            };

            var room = new Room
            {
                Name = "Room",
                HideEmptyConfiguration = true,
                GameObjects = [roomObject]
            };

            var area = new Area { Name = "Area", HideEmptyConfiguration = true, Rooms = [room] };
            var country = new Country { Name = "Country", HideEmptyConfiguration = true, Areas = [area] };
            var planet = new Planet { Name = "Planet", HideEmptyConfiguration = true, Countries = [country] };

            var roomTemplate = new Room { Name = "TemplateRoom", HideEmptyConfiguration = true };
            var globalObject = new GameObject { Name = "GlobalObject", HideEmptyConfiguration = true };
            var objectTemplate = new GameObject { Name = "ObjectTemplate", HideEmptyConfiguration = true };
            var baseObject = new GameObject { Name = "BaseObject", HideEmptyConfiguration = true };

            var project = new ProjectModel
            {
                Name = "HideEmptyConfigRoundTrip",
                HideEmptyConfiguration = true,
                Planets = [planet],
                RoomTemplates = [roomTemplate],
                ObjectTemplates = [objectTemplate],
                BaseObjects = [baseObject],
                GlobalObjectScopeName = "Global Objects",
                GameObjects = [globalObject]
            };

            var projectFilePath = Path.Combine(tempRoot, "HideEmptyConfigRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            Assert.True(loaded!.HideEmptyConfiguration);
            Assert.True(loaded.Planets.Single().HideEmptyConfiguration);
            Assert.True(loaded.Planets.Single().Countries.Single().HideEmptyConfiguration);
            Assert.True(loaded.Planets.Single().Countries.Single().Areas.Single().HideEmptyConfiguration);
            Assert.True(loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().HideEmptyConfiguration);

            var loadedRoomObject = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().GameObjects.Single();
            Assert.True(loadedRoomObject.HideEmptyConfiguration);
            Assert.True(loadedRoomObject.ContainedObjects.Single().HideEmptyConfiguration);

            Assert.True(loaded.GameObjects.Single().HideEmptyConfiguration);
            Assert.True(loaded.ObjectTemplates.Single().HideEmptyConfiguration);
            Assert.True(loaded.BaseObjects.Single().HideEmptyConfiguration);
            Assert.True(loaded.RoomTemplates.Single().HideEmptyConfiguration);
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
    public void TryLoadProjectModel_DefaultsHideEmptyConfigurationFalse_WhenMissingFromPayload()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomObject = new GameObject { Name = "RoomObject", HideEmptyConfiguration = true };
            var room = new Room { Name = "Room", HideEmptyConfiguration = true, GameObjects = [roomObject] };
            var area = new Area { Name = "Area", HideEmptyConfiguration = true, Rooms = [room] };
            var country = new Country { Name = "Country", HideEmptyConfiguration = true, Areas = [area] };
            var planet = new Planet { Name = "Planet", HideEmptyConfiguration = true, Countries = [country] };

            var project = new ProjectModel
            {
                Name = "HideEmptyConfigLegacyDefault",
                HideEmptyConfiguration = true,
                Planets = [planet],
                GlobalObjectScopeName = "Global Objects",
                GameObjects = [new GameObject { Name = "GlobalObject", HideEmptyConfiguration = true }]
            };

            var projectFilePath = Path.Combine(tempRoot, "HideEmptyConfigLegacyDefault.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            foreach (var jsonPath in Directory.EnumerateFiles(tempRoot, "*.json", SearchOption.AllDirectories))
            {
                var node = JsonNode.Parse(File.ReadAllText(jsonPath));
                if (node is null)
                {
                    continue;
                }

                RemovePropertyRecursively(node, "hideEmptyConfiguration");
                File.WriteAllText(jsonPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            Assert.False(loaded!.HideEmptyConfiguration);
            Assert.False(loaded.Planets.Single().HideEmptyConfiguration);
            Assert.False(loaded.Planets.Single().Countries.Single().HideEmptyConfiguration);
            Assert.False(loaded.Planets.Single().Countries.Single().Areas.Single().HideEmptyConfiguration);
            Assert.False(loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().HideEmptyConfiguration);
            Assert.False(loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().GameObjects.Single().HideEmptyConfiguration);
            Assert.False(loaded.GameObjects.Single().HideEmptyConfiguration);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void RemovePropertyRecursively(JsonNode node, string propertyName)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove(propertyName);
                foreach (var child in obj.ToList())
                {
                    if (child.Value is not null)
                    {
                        RemovePropertyRecursively(child.Value, propertyName);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        RemovePropertyRecursively(item, propertyName);
                    }
                }

                break;
        }
    }
}
