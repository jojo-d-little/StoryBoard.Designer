using System.Text.Json;
using System.Text.Json.Nodes;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Rules.Project;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class ElevatorStage3AuthoringTests
{
    [Fact]
    public void ObjectPresentationCueExport_RejectsAmbiguousObjectAndSetTargets()
    {
        var objectId = Guid.NewGuid();
        var action = new CommandAction
        {
            Name = "Invalid cue",
            ActionType = CommandActionType.AddObjectPresentationCue,
            ObjectPresentationCueObjectId = objectId,
            Payload = new AddObjectPresentationCuePayload(
                objectId,
                "Selected",
                new ProjectObjectSetReferenceDto { ObjectSetKey = "liftOccupants" })
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => JsonExportService.ToCommandActionDtoForSequence(action, []));

        Assert.Contains("cannot target both an object and a captured object set", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EngineCatalog_ExposesIntrinsicPropertiesAndDerivedRoomFloorWithoutRoomAuthoringField()
    {
        var room = new Room { Name = "Lift" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            RoomPlacements = [new AreaRoomPlacement { RoomId = room.Id, FloorElevation = -2 }]
        };
        var placedObject = new GameObject { Name = "Player" };
        room.GameObjects.Add(placedObject);
        var project = new ProjectModel
        {
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [area] }] }]
        };

        var choices = MainWindowViewModel.BuildComputedPropertyChoices(project, room);

        var floor = Assert.Single(choices, choice => choice.OwnerId == room.Id && choice.PropertyName == "floorNumber");
        Assert.True(floor.IsEngineProperty);
        Assert.False(floor.CanWrite);
        Assert.Equal(GamePropertyValueRestriction.Numeric, floor.ValueRestriction);
        var name = Assert.Single(choices, choice => choice.OwnerId == room.Id && choice.PropertyName == "nameInGame");
        Assert.False(name.CanWrite);
        var objectName = Assert.Single(choices, choice => choice.OwnerId == placedObject.ObjectId && choice.PropertyName == "nameInGame");
        Assert.False(objectName.CanWrite);
        var scale = Assert.Single(choices, choice => choice.OwnerId == placedObject.ObjectId && choice.PropertyName == "additionalSituationalScale");
        Assert.True(scale.CanWrite);
        Assert.Equal(GamePropertyValueRestriction.Numeric, scale.ValueRestriction);
        Assert.Contains("pivot", scale.Documentation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("neutral value: 1", scale.Documentation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(room.Variables, property => string.Equals(property.Name, "floorNumber", StringComparison.OrdinalIgnoreCase));

        var tempRoot = CreateTempRoot();
        try
        {
            var projectPath = Path.Combine(tempRoot, "ElevatorStage3.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, project);
            var loaded = service.TryLoadProjectModel(projectPath)!;
            var loadedArea = loaded.Planets.Single().Countries.Single().Areas.Single();
            Assert.Equal(-2, loadedArea.RoomPlacements.Single().FloorElevation);
            var areaFile = Directory.GetFiles(tempRoot, "*.area.json", SearchOption.AllDirectories).Single();
            using var areaJson = JsonDocument.Parse(File.ReadAllText(areaFile));
            Assert.Equal(-2, areaJson.RootElement.GetProperty("roomPlacements")[0].GetProperty("floorElevation").GetInt32());
            var roomFile = Directory.GetFiles(tempRoot, "*.room.json", SearchOption.AllDirectories).Single();
            using var roomJson = JsonDocument.Parse(File.ReadAllText(roomFile));
            Assert.False(roomJson.RootElement.TryGetProperty("floorNumber", out _));
            service.ExportCleanProjectV1(projectPath, loaded);
            var runtimeAreaFile = Directory.GetFiles(Path.Combine(tempRoot, "GameRuntimeJson", "Area"), "*.runtime.json").Single();
            using var runtimeArea = JsonDocument.Parse(File.ReadAllText(runtimeAreaFile));
            Assert.Equal(-2, runtimeArea.RootElement.GetProperty("roomPlacements")[0].GetProperty("floorElevation").GetInt32());
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void EngineCatalogPropertyNameCollisions_AreCatalogFirstDeduplicatedAndValidated()
    {
        var room = new Room
        {
            Name = "Lift",
            Variables =
            [
                new GamePropertyDefinition { Name = "nameInGame", DefaultValue = "Authored Lift Name" },
                new GamePropertyDefinition { Name = "floorNumber", DefaultValue = "99" },
                new GamePropertyDefinition { Name = "FloorNumber", DefaultValue = "100" }
            ]
        };
        var project = new ProjectModel
        {
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [new Area { Name = "Area", Rooms = [room] }] }] }]
        };

        var choices = MainWindowViewModel.BuildComputedPropertyChoices(project, room);

        var roomName = Assert.Single(choices, choice => choice.OwnerId == room.Id && choice.PropertyName == "nameInGame");
        Assert.True(roomName.IsEngineProperty);
        Assert.False(roomName.CanWrite);
        var floor = Assert.Single(choices, choice => choice.OwnerId == room.Id && choice.PropertyName == "floorNumber");
        Assert.True(floor.IsEngineProperty);
        Assert.False(floor.CanWrite);

        var issues = new EngineCatalogPropertyNameCollisionRule()
            .Evaluate(ValidationRuleTestContext.Create(project))
            .ToList();
        Assert.Equal(2, issues.Count);
        Assert.All(issues, issue => Assert.Equal(ValidationSeverity.Warning, issue.Severity));
        Assert.Contains(issues, issue => issue.Description.Contains("nameInGame", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Description.Contains("floorNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ObjectSetActionsAndLinkedPlacedObjectTarget_RoundTripThroughProjectAndRuntimeExport()
    {
        var targetObjectId = Guid.NewGuid();
        var explicitCheckTargetId = Guid.NewGuid();
        var root = new CommandAction { Name = "Stage Lift", ActionType = CommandActionType.LinkedActions };
        root.LinkedFlowDefinition = new LinkedActionFlowDefinition
        {
            EntryNodeId = 1,
            Nodes = [new LinkedActionFlowNode { NodeId = 1, ActionId = Guid.NewGuid(), TargetObjectId = targetObjectId }]
        };
        var actions = new List<CommandAction>
        {
            root,
            new() { Name = "Capture occupants", ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
                Payload = new CaptureObjectsOnObjectFootprintPayload("liftOccupants", targetObjectId, FootprintMatchMode.AnyOverlap) },
            new() { Name = "Check player", ActionType = CommandActionType.CheckObjectSetContainsObject,
                Payload = new CheckObjectSetContainsObjectPayload("liftOccupants", "CurrentPlayer") },
            new() { Name = "Check explicit object", ActionType = CommandActionType.CheckObjectSetContainsObject,
                Payload = new CheckObjectSetContainsObjectPayload("liftOccupants", explicitCheckTargetId.ToString("D")) },
            new() { Name = "Ride", ActionType = CommandActionType.NavigateObjectSetToAdjacent,
                Payload = new NavigateObjectSetToAdjacentPayload("liftOccupants", Direction10.North.ToString()) },
            new() { Name = "Cue set", ActionType = CommandActionType.AddObjectPresentationCue,
                Payload = new AddObjectPresentationCuePayload(null, "Selected", new ProjectObjectSetReferenceDto { ObjectSetKey = "liftOccupants" }, PresentationCueLifetime.CurrentRoom) },
            new() { Name = "Remove set cue", ActionType = CommandActionType.RemoveObjectPresentationCue,
                Payload = new RemoveObjectPresentationCuePayload(null, "Selected", new ProjectObjectSetReferenceDto { ObjectSetKey = "liftOccupants" }) },
            new() { Name = "Legacy single cue", ActionType = CommandActionType.AddObjectPresentationCue,
                Payload = new AddObjectPresentationCuePayload(targetObjectId, "Selected") },
            new() { Name = "Current room single cue", ActionType = CommandActionType.AddObjectPresentationCue,
                Payload = new AddObjectPresentationCuePayload(targetObjectId, "Selected", Lifetime: PresentationCueLifetime.CurrentRoom) },
            new() { Name = "Command context cue", ActionType = CommandActionType.AddObjectPresentationCue,
                Payload = new AddObjectPresentationCuePayload(null, "Selected") }
        };
        var membershipAction = actions.Single(action => action.Name == "Check player");
        membershipAction.NormalizeOutcomeMapForActionType(membershipAction.ActionType, preserveUnsupportedTokens: false);
        Assert.Contains("Success", membershipAction.OutcomeMessageMap.Keys);
        Assert.Contains("Failure", membershipAction.OutcomeMessageMap.Keys);
        root.LinkedFlowDefinition.Nodes[0].ActionId = actions[1].Id;
        var project = new ProjectModel { Name = "ElevatorStage3" };
        project.GlobalScope.AvailableActions.AddRange(actions);
        var tempRoot = CreateTempRoot();

        try
        {
            var projectPath = Path.Combine(tempRoot, "ElevatorStage3.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, project);
            var legacyActionFile = Directory.EnumerateFiles(tempRoot, "*.json", SearchOption.AllDirectories)
                .First(path => FindNamedActionJsonNode(JsonNode.Parse(File.ReadAllText(path)), "Legacy single cue") is not null);
            var legacyActionRoot = JsonNode.Parse(File.ReadAllText(legacyActionFile))!;
            FindNamedActionJsonNode(legacyActionRoot, "Legacy single cue")!["payload"]!.AsObject().Remove("lifetime");
            File.WriteAllText(legacyActionFile, legacyActionRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            var loaded = service.TryLoadProjectModel(projectPath)!;
            var loadedActions = loaded.GlobalScope.AvailableActions;

            Assert.Equal(targetObjectId, Assert.IsType<CaptureObjectsOnObjectFootprintPayload>(loadedActions.Single(action => action.ActionType == CommandActionType.CaptureObjectsOnObjectFootprint).Payload).TargetObjectId);
            Assert.Equal("CurrentPlayer", Assert.IsType<CheckObjectSetContainsObjectPayload>(loadedActions.Single(action => action.Name == "Check player").Payload).Target);
            Assert.Equal(explicitCheckTargetId.ToString("D"), Assert.IsType<CheckObjectSetContainsObjectPayload>(loadedActions.Single(action => action.Name == "Check explicit object").Payload).Target);
            Assert.Equal(Direction10.North.ToString(), Assert.IsType<NavigateObjectSetToAdjacentPayload>(loadedActions.Single(action => action.ActionType == CommandActionType.NavigateObjectSetToAdjacent).Payload).Direction);
            var cue = Assert.IsType<AddObjectPresentationCuePayload>(loadedActions.Single(action => action.Name == "Cue set").Payload);
            Assert.Equal("liftOccupants", cue.ObjectSetReference!.ObjectSetKey);
            Assert.Equal(PresentationCueLifetime.CurrentRoom, cue.Lifetime);
            var removeSetCue = Assert.IsType<RemoveObjectPresentationCuePayload>(loadedActions.Single(action => action.Name == "Remove set cue").Payload);
            Assert.Equal("liftOccupants", removeSetCue.ObjectSetReference!.ObjectSetKey);
            var legacySingleCue = Assert.IsType<AddObjectPresentationCuePayload>(loadedActions.Single(action => action.Name == "Legacy single cue").Payload);
            Assert.Equal(targetObjectId, legacySingleCue.ObjectId);
            Assert.Equal(PresentationCueLifetime.UntilRemoved, legacySingleCue.Lifetime);
            Assert.Equal(PresentationCueLifetime.CurrentRoom, Assert.IsType<AddObjectPresentationCuePayload>(loadedActions.Single(action => action.Name == "Current room single cue").Payload).Lifetime);
            service.SaveProjectModel(projectPath, loaded);
            loaded = service.TryLoadProjectModel(projectPath)!;
            Assert.Equal(PresentationCueLifetime.UntilRemoved, Assert.IsType<AddObjectPresentationCuePayload>(loaded.GlobalScope.AvailableActions.Single(action => action.Name == "Legacy single cue").Payload).Lifetime);
            Assert.Null(Assert.IsType<AddObjectPresentationCuePayload>(loadedActions.Single(action => action.Name == "Command context cue").Payload).ObjectId);
            Assert.Equal(targetObjectId, loadedActions.Single(action => action.Name == "Stage Lift").LinkedFlowDefinition.Nodes.Single().TargetObjectId);

            var runtimePath = service.ExportCleanProjectV1(projectPath, loaded);
            using var runtime = JsonDocument.Parse(File.ReadAllText(runtimePath));
            var runtimeActions = runtime.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();
            Assert.Equal(targetObjectId, runtimeActions.Single(action => action.GetProperty("actionType").GetString() == nameof(CommandActionType.CaptureObjectsOnObjectFootprint))
                .GetProperty("payload").GetProperty("targetObjectId").GetGuid());
            Assert.Equal("CurrentPlayer", runtimeActions.Single(action => action.GetProperty("name").GetString() == "Check player")
                .GetProperty("payload").GetProperty("target").GetString());
            Assert.Equal("liftOccupants", runtimeActions.Single(action => action.GetProperty("name").GetString() == "Cue set")
                .GetProperty("payload").GetProperty("objectSetReference").GetProperty("objectSetKey").GetString());
            Assert.Equal(nameof(PresentationCueLifetime.CurrentRoom), runtimeActions.Single(action => action.GetProperty("name").GetString() == "Cue set")
                .GetProperty("payload").GetProperty("lifetime").GetString());
            Assert.Equal("liftOccupants", runtimeActions.Single(action => action.GetProperty("name").GetString() == "Remove set cue")
                .GetProperty("payload").GetProperty("objectSetReference").GetProperty("objectSetKey").GetString());
            Assert.Equal(targetObjectId, runtimeActions.Single(action => action.GetProperty("name").GetString() == "Legacy single cue")
                .GetProperty("payload").GetProperty("objectId").GetGuid());
            Assert.Equal(nameof(PresentationCueLifetime.UntilRemoved), runtimeActions.Single(action => action.GetProperty("name").GetString() == "Legacy single cue")
                .GetProperty("payload").GetProperty("lifetime").GetString());
            Assert.Equal(nameof(PresentationCueLifetime.CurrentRoom), runtimeActions.Single(action => action.GetProperty("name").GetString() == "Current room single cue")
                .GetProperty("payload").GetProperty("lifetime").GetString());
            var commandContextCue = runtimeActions.Single(action => action.GetProperty("name").GetString() == "Command context cue")
                .GetProperty("payload");
            Assert.False(commandContextCue.TryGetProperty("objectId", out _));
            Assert.False(commandContextCue.TryGetProperty("objectSetReference", out _));
            Assert.Equal(targetObjectId, runtimeActions.Single(action => action.GetProperty("name").GetString() == "Stage Lift")
                .GetProperty("payload").GetProperty("linkedActions")[0].GetProperty("targetObjectId").GetGuid());
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static JsonNode? FindNamedActionJsonNode(JsonNode? node, string actionName)
    {
        if (node is JsonObject obj
            && obj["name"]?.GetValue<string>() == actionName
            && obj["payload"] is not null)
        {
            return obj;
        }

        if (node is JsonObject objectNode)
        {
            foreach (var child in objectNode.Select(static pair => pair.Value))
            {
                var result = FindNamedActionJsonNode(child, actionName);
                if (result is not null) return result;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                var result = FindNamedActionJsonNode(child, actionName);
                if (result is not null) return result;
            }
        }

        return null;
    }

    [Fact]
    public void PreRoomSetupAdvisory_AllowsWritableCatalogSetAndSynchronousCheck_RecursesWithNestedPaths()
    {
        var target = new GameObject { Name = "Destination Elevator" };
        var writableSet = new CommandAction
        {
            Name = "Stage scale",
            ActionType = CommandActionType.SetGameProperty,
            Payload = new SetGamePropertyPayload("additionalSituationalScale", "0.5",
                new ProjectGamePropertySourceReferenceDto { OwnerId = target.ObjectId, PropertyName = "additionalSituationalScale" })
        };
        var check = new CommandAction
        {
            Name = "Check floor",
            ActionType = CommandActionType.CheckGameProperty,
            Payload = new CheckGamePropertyPayload("floorNumber", true,
                new ProjectGamePropertySourceReferenceDto { AnchorKey = "currentRoom", PropertyName = "floorNumber" })
        };
        var room = new Room { Name = "Upper Lift", GameObjects = [target] };
        var readOnlySet = new CommandAction
        {
            Name = "Write floor",
            ActionType = CommandActionType.SetGameProperty,
            Payload = new SetGamePropertyPayload("floorNumber", "2",
                new ProjectGamePropertySourceReferenceDto { OwnerId = room.Id, PropertyName = "floorNumber" })
        };
        var navigation = new CommandAction { Name = "Navigate", ActionType = CommandActionType.NavigateDirection };
        var setup = new CommandAction { Name = "Elevator setup", ActionType = CommandActionType.LinkedActions };
        setup.LinkedFlowDefinition = new LinkedActionFlowDefinition
        {
            EntryNodeId = 1,
            Nodes =
            [
                new LinkedActionFlowNode { NodeId = 1, ActionId = writableSet.Id, TargetObjectId = target.ObjectId },
                new LinkedActionFlowNode { NodeId = 2, ActionId = check.Id },
                new LinkedActionFlowNode { NodeId = 3, ActionId = readOnlySet.Id },
                new LinkedActionFlowNode { NodeId = 4, ActionId = navigation.Id }
            ]
        };
        room.PreRoomEntryActionId = setup.Id;
        room.AvailableActions.Add(setup);
        room.AvailableActions.Add(writableSet);
        room.AvailableActions.Add(check);
        room.AvailableActions.Add(readOnlySet);
        room.AvailableActions.Add(navigation);
        var project = new ProjectModel
        {
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [new Area { Name = "Area", Rooms = [room] }] }] }]
        };

        var issues = new PreRoomEntrySetupAdvisoryRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        Assert.Equal(3, issues.Count);
        Assert.All(issues, issue => Assert.Equal(ValidationSeverity.Warning, issue.Severity));
        Assert.Contains(issues, issue => issue.Description.Contains("currentRoom", StringComparison.OrdinalIgnoreCase)
            && issue.Path.Contains("Check floor (CheckGameProperty)", StringComparison.Ordinal));
        var readOnlyIssue = Assert.Single(issues, issue => issue.Path.Contains("Write floor (SetGameProperty)", StringComparison.Ordinal));
        Assert.Contains("cataloged writable Engine property", readOnlyIssue.Description, StringComparison.OrdinalIgnoreCase);
        var navigationIssue = Assert.Single(issues, issue => issue.Path.Contains("Navigate (NavigateDirection)", StringComparison.Ordinal));
        Assert.Contains("Linked Flow Node 4", navigationIssue.Path, StringComparison.Ordinal);
        Assert.Contains("Navigation can redirect", navigationIssue.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void PreRoomSetupAdvisory_DirectStartTimerIsReviewed_AndCallbackIsNotTreatedAsSetup()
    {
        var sequenceId = Guid.NewGuid();
        var callback = new CommandAction { Name = "Start ride", ActionType = CommandActionType.StartSequence };
        ActionPayloadAccessors.SetStartSequenceId(callback, sequenceId);
        var startTimer = new CommandAction
        {
            Name = "Queue ride",
            ActionType = CommandActionType.StartTimer,
            Payload = new StartTimerPayload("lift.start", TimerOwnerType.Room)
        };
        var room = new Room { Name = "Lift", PreRoomEntryActionId = startTimer.Id };
        room.AvailableActions.Add(startTimer);
        room.AvailableActions.Add(callback);
        room.TimerDefinitions.Add(new RuntimeTimerDefinitionDto
        {
            TimerKey = "lift.start",
            ScheduleAfterMs = 50,
            FireMode = TimerFireMode.OneShot,
            TargetActionRef = callback.Id.ToString("D"),
            LifetimeOwnerType = TimerOwnerType.Room,
            ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
            Enabled = true
        });
        room.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto
        {
            SequenceId = sequenceId,
            DisplayName = "Lift ride"
        });
        var project = new ProjectModel { Name = "StartTimerSetup", RoomTemplates = [room] };

        var issues = new PreRoomEntrySetupAdvisoryRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        Assert.Empty(issues);

        var tempRoot = CreateTempRoot();
        try
        {
            var projectPath = Path.Combine(tempRoot, "StartTimerSetup.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, project);
            var loadedRoom = service.TryLoadProjectModel(projectPath)!.RoomTemplates.Single();
            var loadedTimer = Assert.Single(loadedRoom.TimerDefinitions);
            var loadedStartTimer = loadedRoom.AvailableActions.Single(action => action.Name == "Queue ride");
            var loadedCallback = loadedRoom.AvailableActions.Single(action => action.Name == "Start ride");

            Assert.Equal("lift.start", loadedTimer.TimerKey);
            Assert.Equal(TimerOwnerType.Room, loadedTimer.LifetimeOwnerType);
            Assert.Equal(callback.Id, loadedCallback.Id);
            Assert.Equal(callback.Id.ToString("D"), loadedTimer.TargetActionRef);
            Assert.Equal(sequenceId, loadedRoom.SequenceDefinitions.Single().SequenceId);
            Assert.Equal(sequenceId, ActionPayloadAccessors.GetStartSequenceId(loadedCallback));
            Assert.Equal("lift.start", ActionPayloadAccessors.GetStartTimerKey(loadedStartTimer));
            Assert.Equal(TimerOwnerType.Room, ActionPayloadAccessors.GetStartTimerOwnerScopeKindOverride(loadedStartTimer));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PreRoomSetupAdvisory_NestedStartTimerIsReviewed_WhileOtherNestedLeavesRemainAdvisory()
    {
        var callback = new CommandAction { Name = "Start ride", ActionType = CommandActionType.StartSequence };
        var startTimer = new CommandAction
        {
            Name = "Queue ride",
            ActionType = CommandActionType.StartTimer,
            Payload = new StartTimerPayload("lift.start", TimerOwnerType.Room)
        };
        var navigation = new CommandAction { Name = "Navigate", ActionType = CommandActionType.NavigateDirection };
        var setup = new CommandAction { Name = "Lift setup", ActionType = CommandActionType.LinkedActions };
        setup.LinkedFlowDefinition = new LinkedActionFlowDefinition
        {
            EntryNodeId = 1,
            Nodes =
            [
                new LinkedActionFlowNode { NodeId = 1, ActionId = startTimer.Id },
                new LinkedActionFlowNode { NodeId = 2, ActionId = navigation.Id }
            ]
        };
        var room = new Room { Name = "Lift", PreRoomEntryActionId = setup.Id };
        room.AvailableActions.Add(setup);
        room.AvailableActions.Add(startTimer);
        room.AvailableActions.Add(callback);
        room.AvailableActions.Add(navigation);
        room.TimerDefinitions.Add(new RuntimeTimerDefinitionDto
        {
            TimerKey = "lift.start",
            ScheduleAfterMs = 50,
            FireMode = TimerFireMode.OneShot,
            TargetActionRef = callback.Id.ToString("D"),
            LifetimeOwnerType = TimerOwnerType.Room,
            ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
            Enabled = true
        });
        var project = new ProjectModel { RoomTemplates = [room] };

        var issues = new PreRoomEntrySetupAdvisoryRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        var issue = Assert.Single(issues);
        Assert.Contains("Navigate (NavigateDirection)", issue.Path, StringComparison.Ordinal);
        Assert.Contains("Navigation can redirect", issue.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(issues, item => item.Path.Contains("Queue ride", StringComparison.Ordinal)
            || item.Path.Contains("Start ride", StringComparison.Ordinal));
    }

    [Fact]
    public void PreRoomSetupAdvisory_UnknownLeafGetsGenericFallbackWarning()
    {
        var unknown = new CommandAction { Name = "Future action", ActionType = (CommandActionType)98765 };
        var room = new Room { Name = "Lift", PreRoomEntryActionId = unknown.Id };
        room.AvailableActions.Add(unknown);
        var project = new ProjectModel
        {
            RoomTemplates = [room]
        };

        var issue = Assert.Single(new PreRoomEntrySetupAdvisoryRule().Evaluate(ValidationRuleTestContext.Create(project)));

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("not been reviewed for pre-room-entry setup", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Future action (98765)", issue.Path, StringComparison.Ordinal);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
