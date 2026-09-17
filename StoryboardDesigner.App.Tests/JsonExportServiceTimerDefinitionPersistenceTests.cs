using System.Text;
using System.Text.Json;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceTimerDefinitionPersistenceTests
{
    private const string RuntimeExportFolderName = "GameRuntimeJson";

    [Fact]
    public void SaveProjectModel_RoundTripsScopeTimerDefinitions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Timer Room"
            };
            room.TimerDefinitions.Add(new RuntimeTimerDefinitionDto
            {
                TimerKey = "room.delay",
                ScheduleAfterMs = 500,
                FireMode = TimerFireMode.OneShot,
                TargetActionRef = "OpenDoor",
                LifetimeOwnerType = TimerOwnerType.Room,
                ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
                Enabled = true
            });

            var area = new Area
            {
                Name = "Timer Area",
                Rooms = new List<Room> { room }
            };
            var country = new Country
            {
                Name = "Timer Country",
                Areas = new List<Area> { area }
            };
            var planet = new Planet
            {
                Name = "Timer Planet",
                Countries = new List<Country> { country }
            };

            var project = new ProjectModel
            {
                Name = "TimerRoundTrip",
                Planets = new List<Planet> { planet }
            };
            project.GlobalScope.TimerDefinitions.Add(new RuntimeTimerDefinitionDto
            {
                TimerKey = "global.pulse",
                ScheduleAfterMs = 300,
                FireMode = TimerFireMode.Repeating,
                RepeatMode = TimerRepeatMode.GrowingInterval,
                RepeatProgressionMode = TimerRepeatProgressionMode.Linear,
                RepeatIntervalMs = 1000,
                RepeatIntervalStepMs = 250,
                TargetActionRef = "PulseAction",
                LifetimeOwnerType = TimerOwnerType.Session,
                ConflictBehavior = TimerConflictBehavior.IgnoreIfExists,
                Enabled = true
            });

            var projectFilePath = Path.Combine(tempRoot, "TimerRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedGlobalTimer = Assert.Single(loaded!.GlobalScope.TimerDefinitions);
            Assert.Equal("global.pulse", loadedGlobalTimer.TimerKey);
            Assert.Equal(TimerFireMode.Repeating, loadedGlobalTimer.FireMode);
            Assert.Equal(TimerRepeatMode.GrowingInterval, loadedGlobalTimer.RepeatMode);
            Assert.Equal(TimerRepeatProgressionMode.Linear, loadedGlobalTimer.RepeatProgressionMode);
            Assert.Equal(1000, loadedGlobalTimer.RepeatIntervalMs);
            Assert.Equal(250, loadedGlobalTimer.RepeatIntervalStepMs);
            Assert.Equal("PulseAction", loadedGlobalTimer.TargetActionRef);
            Assert.Equal(TimerOwnerType.Session, loadedGlobalTimer.LifetimeOwnerType);
            Assert.Equal(TimerConflictBehavior.IgnoreIfExists, loadedGlobalTimer.ConflictBehavior);

            var loadedRoom = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var loadedRoomTimer = Assert.Single(loadedRoom.TimerDefinitions);
            Assert.Equal("room.delay", loadedRoomTimer.TimerKey);
            Assert.Equal(TimerFireMode.OneShot, loadedRoomTimer.FireMode);
            Assert.Equal("OpenDoor", loadedRoomTimer.TargetActionRef);
            Assert.Equal(TimerOwnerType.Room, loadedRoomTimer.LifetimeOwnerType);
            Assert.Equal(TimerConflictBehavior.ReplaceExisting, loadedRoomTimer.ConflictBehavior);
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
    public void ExportRuntimeProjectV1_EmitsScopeTimerDefinitions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Timer Room"
            };
            room.TimerDefinitions.Add(new RuntimeTimerDefinitionDto
            {
                TimerKey = "room.loop",
                ScheduleAfterMs = 200,
                FireMode = TimerFireMode.Repeating,
                RepeatMode = TimerRepeatMode.FixedInterval,
                RepeatIntervalMs = 450,
                TargetActionRef = "RoomTick",
                LifetimeOwnerType = TimerOwnerType.Room,
                ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
                Enabled = true
            });

            var area = new Area
            {
                Name = "Timer Area",
                Rooms = new List<Room> { room }
            };
            var country = new Country
            {
                Name = "Timer Country",
                Areas = new List<Area> { area }
            };
            var planet = new Planet
            {
                Name = "Timer Planet",
                Countries = new List<Country> { country }
            };

            var project = new ProjectModel
            {
                Name = "TimerRuntimeExport",
                Planets = new List<Planet> { planet }
            };
            project.GlobalScope.TimerDefinitions.Add(new RuntimeTimerDefinitionDto
            {
                TimerKey = "global.tick",
                ScheduleAfterMs = 120,
                FireMode = TimerFireMode.OneShot,
                TargetActionRef = "GlobalTick",
                LifetimeOwnerType = TimerOwnerType.Session,
                ConflictBehavior = TimerConflictBehavior.IgnoreIfExists,
                Enabled = true
            });

            var projectFilePath = Path.Combine(tempRoot, "TimerRuntimeExport.sbe.json");
            var cleanProjectPath = service.ExportCleanProjectV1(projectFilePath, project);

            using var rootDoc = JsonDocument.Parse(File.ReadAllText(cleanProjectPath, Encoding.UTF8));
            var rootTimers = rootDoc.RootElement.GetProperty("timerDefinitions");
            Assert.Equal(1, rootTimers.GetArrayLength());
            Assert.Equal("global.tick", rootTimers[0].GetProperty("timerKey").GetString());

            var roomScopePath = Path.Combine(
                Path.GetDirectoryName(projectFilePath)!,
                RuntimeExportFolderName,
                "Room",
                $"{room.Id:D}.runtime.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomScopePath, Encoding.UTF8));
            var roomTimers = roomDoc.RootElement.GetProperty("timerDefinitions");
            Assert.Equal(1, roomTimers.GetArrayLength());
            Assert.Equal("room.loop", roomTimers[0].GetProperty("timerKey").GetString());
            Assert.Equal("RoomTick", roomTimers[0].GetProperty("targetActionRef").GetString());
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
