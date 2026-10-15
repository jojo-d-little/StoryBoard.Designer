using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class SpatialAudioAuthoringPersistenceTests
{
    [Fact]
    public void LoadLegacyProject_DefaultsSpatialAudioFieldsSafely()
    {
        var legacyProject = SampleProjectFixtureLoader.LoadSingleRoomFixture();
        var room = Assert.Single(legacyProject.Planets.SelectMany(static planet => planet.Countries)
            .SelectMany(static country => country.Areas)
            .SelectMany(static area => area.Rooms));

        Assert.Null(legacyProject.RoomSoundLossDefaults);
        Assert.Equal(new RoomSpawnPoint(400, 300, IsDefault: true), room.SpawnPoint);
        Assert.Equal(RoomEnvironmentType.Indoor, room.EnvironmentType);
        Assert.Null(room.SoundLossPercentOverride);
        Assert.All(EnumerateObjects(room.GameObjects), static obj => Assert.Empty(obj.SoundEmitters));
    }

    [Fact]
    public void SaveAndCleanExport_PreserveSpatialAudioAndRoomLocationFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var soundId = Guid.NewGuid();
            var emitter = new ObjectSoundEmitter
            {
                SoundEffectId = soundId,
                ActivationGamePropertyName = "isPlaying",
                ActiveInRoom = true,
                ActiveOutOfRoom = true
            };
            var obj = new GameObject
            {
                Name = "Rain Barrel",
                SoundEffectLibraryEntries =
                [
                    new SoundEffectLibraryEntry
                    {
                        SoundEffectId = soundId,
                        SoundEffectKey = "rain.barrel",
                        DisplayName = "Rain Barrel",
                        RepeatMode = "UntilCanceled",
                        RepeatIntervalMinMs = 1200,
                        RepeatIntervalMaxMs = 2600,
                        MaxInRoomDistancePixels = 450
                    }
                ],
                SoundEmitters = [emitter],
                Variables = [new GamePropertyDefinition { Name = "isPlaying", DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse }]
            };
            var room = new Room
            {
                Name = "Courtyard",
                SpawnPoint = new RoomSpawnPoint(123.5, 456.25),
                EnvironmentType = RoomEnvironmentType.Outdoor,
                SoundLossPercentOverride = 18,
                GameObjects = [obj]
            };
            var project = new ProjectModel
            {
                Name = "SpatialAudioRoundTrip",
                RoomSoundLossDefaults = new RoomSoundLossDefaults { IndoorSoundLossPercent = 42, OutdoorSoundLossPercent = 7 },
                Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [new Area { Name = "Area", Rooms = [room] }] }] }]
            };
            var projectFilePath = Path.Combine(tempRoot, "SpatialAudioRoundTrip.sbe.json");

            service.SaveProjectModel(projectFilePath, project);
            var loaded = service.TryLoadProjectModel(projectFilePath)!;
            var loadedRoom = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var loadedObject = loadedRoom.GameObjects.Single();
            Assert.Equal(new RoomSpawnPoint(123.5, 456.25, IsDefault: true), loadedRoom.SpawnPoint);
            Assert.Equal(RoomEnvironmentType.Outdoor, loadedRoom.EnvironmentType);
            Assert.Equal(18, loadedRoom.SoundLossPercentOverride);
            Assert.Equal(42, loaded.RoomSoundLossDefaults!.IndoorSoundLossPercent);
            Assert.Equal(7, loaded.RoomSoundLossDefaults.OutdoorSoundLossPercent);
            Assert.Equal("isPlaying", loadedObject.SoundEmitters.Single().ActivationGamePropertyName);
            Assert.Equal(soundId, loadedObject.SoundEffectLibraryEntries.Single().SoundEffectId);
            Assert.Equal(1200, loadedObject.SoundEffectLibraryEntries.Single().RepeatIntervalMinMs);
            Assert.Equal(2600, loadedObject.SoundEffectLibraryEntries.Single().RepeatIntervalMaxMs);
            Assert.Equal(450, loadedObject.SoundEffectLibraryEntries.Single().MaxInRoomDistancePixels);

            service.ExportCleanProjectV1(projectFilePath, project);
            var runtimeProjectPath = Path.Combine(tempRoot, "GameRuntimeJson", "SpatialAudioRoundTrip.sbr.runtime.json");
            using var runtimeProject = JsonDocument.Parse(File.ReadAllText(runtimeProjectPath));
            var runtimeDefaults = runtimeProject.RootElement.GetProperty("roomSoundLossDefaults");
            Assert.Equal(42, runtimeDefaults.GetProperty("indoorSoundLossPercent").GetDouble());
            Assert.Equal(7, runtimeDefaults.GetProperty("outdoorSoundLossPercent").GetDouble());
            var runtimeRoomPath = Path.Combine(tempRoot, "GameRuntimeJson", "Room", room.Id.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtimeRoom = JsonDocument.Parse(File.ReadAllText(runtimeRoomPath));
            var roomJson = runtimeRoom.RootElement;
            Assert.Equal(123.5, roomJson.GetProperty("spawnPoints").EnumerateArray().Single().GetProperty("x").GetDouble());
            Assert.Equal("Outdoor", roomJson.GetProperty("environmentType").GetString());
            Assert.Equal(18, roomJson.GetProperty("soundLossPercentOverride").GetDouble());
            var runtimeObjectPath = Path.Combine(tempRoot, "GameRuntimeJson", "GameObject", obj.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtimeObjectDoc = JsonDocument.Parse(File.ReadAllText(runtimeObjectPath));
            var runtimeObject = runtimeObjectDoc.RootElement;
            Assert.Equal(emitter.EmitterId.ToString(), runtimeObject.GetProperty("soundEmitters")[0].GetProperty("emitterId").GetString(), ignoreCase: true);
            Assert.True(runtimeObject.GetProperty("soundEmitters")[0].GetProperty("activeOutOfRoom").GetBoolean());
            var runtimeSound = runtimeObject.GetProperty("soundEffectLibraryEntries")[0];
            Assert.Equal(1200, runtimeSound.GetProperty("repeatIntervalMinMs").GetInt32());
            Assert.Equal(2600, runtimeSound.GetProperty("repeatIntervalMaxMs").GetInt32());
            Assert.Equal(450, runtimeSound.GetProperty("maxInRoomDistancePixels").GetDouble());
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static IEnumerable<GameObject> EnumerateObjects(IEnumerable<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            yield return obj;
            foreach (var child in EnumerateObjects(obj.ContainedObjects)) yield return child;
        }
    }
}
