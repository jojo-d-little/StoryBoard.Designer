using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameManager;
using Storyboard.Shared.GameServices.Commands;
using Storyboard.Shared.GameServices.Bootstrap;
using Storyboard.Shared.GameServices.Events;
using Storyboard.Shared.GameServices.RuntimeContext;
using Storyboard.Shared.GameServices.Sequences;
using Storyboard.Shared.GameServices.ThinkLoop;
using Storyboard.Shared.GameServices.Mutations;
using Storyboard.Shared.GameServices.Timers;
using Storyboard.Shared.GameStateData;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using Storyboard.Shared.RuntimeContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class GeographyOwnedObjectSetRuntimeAcceptanceTests
{
    [Fact]
    public void DesignerExport_CapturesAreaMembersMovesThemAndEventStartedSequenceChecksCuesAndReleases()
    {
        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var navigationSequenceId = Guid.NewGuid();
        var targetId = Guid.Parse("a1030000-0000-0000-0000-000000000001");
        var playerId = Guid.Parse("a1030000-0000-0000-0000-000000000002");
        var companionId = Guid.Parse("a1030000-0000-0000-0000-000000000003");
        var bystanderId = Guid.Parse("a1030000-0000-0000-0000-000000000004");
        var roomShadowTargetId = Guid.Parse("a1030000-0000-0000-0000-000000000005");
        var producerSequenceId = Guid.NewGuid();
        var consumerSequenceId = Guid.NewGuid();
        var source = new Room
        {
            Id = sourceId,
            Name = "Source",
            SpawnPoints = [new RoomSpawnPoint(0, 0, IsDefault: true)],
            GameObjects =
            [
                Object("Footprint", targetId, 0, 0, 2),
                Player("Player", playerId, 0, 0),
                Object("Companion", companionId, 40, 0)
            ]
        };
        var destination = new Room
        {
            Id = destinationId,
            Name = "Destination",
            GameObjects =
            [
                Object("Destination bystander", bystanderId, 0, 0),
                Object("Room shadow target", roomShadowTargetId, 120, 120)
            ]
        };
        var area = new Area
        {
            Id = areaId,
            Name = "Area",
            StartingRoomId = sourceId,
            Rooms = [source, destination],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = sourceId,
                    RoomBId = destinationId,
                    BaseTraversalDirectionFromA = Direction10.East
                }
            ],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = sourceId, X = 0, Y = 0 },
                new AreaRoomPlacement { RoomId = destinationId, X = 1, Y = 0 }
            ]
        };

        var capture = new CommandAction
        {
            Name = "Capture trip members",
            ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
            Payload = new CaptureObjectsOnObjectFootprintPayload("tripMembers", targetId, FootprintMatchMode.AnyOverlap, ObjectSetCaptureLifetime.Area)
        };
        source.SequenceDefinitions =
        [
            new ProjectSequenceDefinitionDto
            {
                SequenceId = producerSequenceId,
                DisplayName = "Capture producer",
                LifetimeOwnerType = TimerOwnerType.Room,
                Steps = [ActionStep(capture, isEnd: true)]
            }
        ];
        var navigator = new CommandAction
        {
            Name = "Move captured group",
            ActionType = CommandActionType.NavigateObjectSetToAdjacent,
            Payload = new NavigateObjectSetToAdjacentPayload("tripMembers", "East")
        };
        source.AvailableActions.Add(navigator);
        source.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto
        {
            SequenceId = navigationSequenceId,
            DisplayName = "Navigate after capture producer",
            LifetimeOwnerType = TimerOwnerType.Room,
            Steps = [ActionStep(navigator, isEnd: true)]
        });
        var check = new CommandAction
        {
            Name = "Check player membership",
            ActionType = CommandActionType.CheckObjectSetContainsObject,
            Payload = new CheckObjectSetContainsObjectPayload("tripMembers", "CurrentPlayer")
        };
        var addCue = new CommandAction
        {
            Name = "Add member cue",
            ActionType = CommandActionType.AddObjectPresentationCue,
            Payload = new AddObjectPresentationCuePayload(null, "scale.grow.medium", new() { ObjectSetKey = "tripMembers" })
        };
        var removeCue = new CommandAction
        {
            Name = "Remove member cue",
            ActionType = CommandActionType.RemoveObjectPresentationCue,
            Payload = new RemoveObjectPresentationCuePayload(null, "scale.grow.medium", new() { ObjectSetKey = "tripMembers" })
        };
        var release = new CommandAction
        {
            Name = "Release trip members",
            ActionType = CommandActionType.ReleaseObjectSet,
            Payload = new ReleaseObjectSetPayload("tripMembers")
        };
        var checkStepId = Guid.NewGuid();
        var addCueStepId = Guid.NewGuid();
        var removeCueStepId = Guid.NewGuid();
        var releaseStepId = Guid.NewGuid();
        destination.SequenceDefinitions =
        [
            new ProjectSequenceDefinitionDto
            {
                SequenceId = consumerSequenceId,
                DisplayName = "Event consumer",
                LifetimeOwnerType = TimerOwnerType.Room,
                Steps =
                [
                    ActionStep(check, checkStepId, addCueStepId),
                    ActionStep(addCue, addCueStepId, removeCueStepId),
                    ActionStep(removeCue, removeCueStepId, releaseStepId),
                    ActionStep(release, releaseStepId, isEnd: true)
                ]
            }
        ];
        var startConsumer = new CommandAction
        {
            Name = "Start event consumer",
            ActionType = CommandActionType.StartSequence,
            NoVerbLinkage = true,
            Payload = new StartSequencePayload(consumerSequenceId)
        };
        destination.AvailableActions.Add(startConsumer);
        destination.EventSubscriptions =
        [
            new EventSubscriptionDefinition
            {
                Id = Guid.NewGuid(),
                EventKey = RuntimeEventKey.player_room_entered.ToString(),
                SubscriptionName = "Start retained member consumer",
                IsEnabled = true,
                ActionBindings = [new EventActionBindingDefinition { Order = 0, Target = new EventBindingTargetDefinition { ActionName = startConsumer.Name } }]
            }
        ];
        var project = new ProjectModel
        {
            Name = "Geography set acceptance",
            RoomDesignerGridCellSize = 40,
            RoomImageCanvasWidth = 400,
            RoomImageCanvasHeight = 400,
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [area] }] }]
        };
        ScopeHierarchy.AttachParents(project);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var projectPath = Path.Combine(tempRoot, "acceptance.sbe.json");
        try
        {
            var exportService = new JsonExportService();
            exportService.SaveProjectModel(projectPath, project);
            var reopened = Assert.IsType<ProjectModel>(exportService.TryLoadProjectModel(projectPath));
            var runtimeProjectPath = exportService.ExportCleanProjectV1(projectPath, reopened);
            var runtimeDocuments = string.Join("\n", Directory.EnumerateFiles(Path.GetDirectoryName(runtimeProjectPath)!, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText));
            Assert.Contains("\"captureLifetime\":\"Area\"", runtimeDocuments, StringComparison.Ordinal);

            var loaded = new CleanExportRuntimeLoadedGameLoader().TryLoad(runtimeProjectPath);
            Assert.NotNull(loaded);
            var session = GameStateSession.Create(loaded!.WorldSnapshot);
            Assert.Equal(sourceId, session.CurrentRoom!.ScopeNodeId);
            var startRequest = new RuntimeCommandProcessingRequest { Session = session, CommandText = "begin trip" };
            var started = session.SequenceManager.TryStart(session, session.CurrentRoom, producerSequenceId, startRequest, new RuntimeActionInvocationContext());
            Assert.True(started.Success, started.Diagnostic);

            var dispatch = new RuntimeSequenceDispatchLoop();
            Assert.True(dispatch.TryProcessNextDueGroup(session, out _, out var captureResult));
            Assert.True(captureResult.Success, string.Join(" | ", captureResult.Diagnostics));
            Assert.Equal(RuntimeSequenceRunStatus.Succeeded, started.Run!.Status);
            var areaNode = session.EnumerateRuntimeScopeNodes().Single(node => node.ScopeNodeId == areaId);
            var capturedSets = (System.Collections.IDictionary)areaNode.GetType()
                .GetProperty("CapturedObjectSets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(areaNode)!;
            var capturedIds = (IReadOnlyList<Guid>)capturedSets["tripMembers"]!;
            Assert.Equal(2, capturedIds.Count);
            Assert.Equal(new[] { playerId, companionId }.OrderBy(id => id), capturedIds.OrderBy(id => id));
            Assert.DoesNotContain(bystanderId, capturedIds);
            var executor = new RuntimeCommandActionExecutor(new ActionScriptEvaluationService());

            var navigationStarted = session.SequenceManager.TryStart(session, session.CurrentRoom,
                navigationSequenceId, startRequest, new RuntimeActionInvocationContext());
            Assert.True(navigationStarted.Success, navigationStarted.Diagnostic);
            var processor = new GameCommandProcessorService(new ActionScriptEvaluationService(), new GameCommandPreprocessorService());
            using var manager = CreateRuntimeGameManager(new CleanExportRuntimeLoadedGameLoader(), processor, session, out var pendingEvents);
            var thinkLoop = new GameThinkLoop(
                new GameThinkLoopCommandQueue(),
                processor,
                new RuntimeEventDispatchLoop(pendingEvents, new RuntimeEventSubscriptionDispatcher()),
                pendingEvents,
                new RuntimeMutationSummaryService(processor),
                timerDispatchLoop: new RuntimeTimerDispatchLoop(),
                sequenceDispatchLoop: new RuntimeSequenceDispatchLoop(actionExecutionService: new RuntimeActionExecutionService(new ActionScriptEvaluationService())));
            var wake = thinkLoop.ProcessWake(new GameThinkLoopWakeRequest
            {
                Session = session,
                WakeTimeBudgetMs = 5000,
                MaxWorkItemsPerWake = 32,
                MaxWorkItemsPerChain = 32,
                MaxCatchUpTicksPerWake = 8
            });
            Assert.Equal(destinationId, session.CurrentRoom!.ScopeNodeId);
            Assert.Empty(session.GetPersistentPresentationCues(playerId));
            var consumerGroup = Assert.Single(wake.DispatchedSequenceGroups, group => group.ProcessedGroup.SequenceId == consumerSequenceId);
            Assert.True(consumerGroup.DispatchResult.Success, string.Join(" | ", consumerGroup.DispatchResult.Diagnostics));
            Assert.Contains(wake.DispatchedEvents, item => item.PublishedEvent.EventKey == RuntimeEventKey.player_room_entered);
            Assert.False(IsMember(executor, session, playerId, "tripMembers"));
            Assert.Empty(session.GetPersistentPresentationCues(playerId));
            Assert.Empty(session.GetPersistentPresentationCues(companionId));

            var recaptured = CaptureSet(executor, session, "tripMembers", bystanderId, ObjectSetCaptureLifetime.Area);
            Assert.True(recaptured.IsLogicalSuccess);
            var areaSets = GetCapturedObjectSets(session.EnumerateRuntimeScopeNodes().Single(node => node.ScopeNodeId == areaId));
            Assert.True(areaSets.Contains("tripMembers"));
            // Seed distinct snapshots to isolate nearest-scope shadowing from destination capture eligibility.
            areaSets["tripMembers"] = new[] { bystanderId };
            var roomSets = GetCapturedObjectSets(session.CurrentRoom!);
            Assert.True(CaptureSet(executor, session, "tripMembers", roomShadowTargetId, ObjectSetCaptureLifetime.Room).IsLogicalSuccess);
            roomSets["tripMembers"] = new[] { roomShadowTargetId };
            Assert.True(IsMember(executor, session, roomShadowTargetId, "tripMembers"));
            Assert.False(IsMember(executor, session, bystanderId, "tripMembers"));
            Assert.True(ReleaseSet(executor, session, "tripMembers").IsLogicalSuccess);
            Assert.True(IsMember(executor, session, bystanderId, "tripMembers"));
            Assert.False(IsMember(executor, session, roomShadowTargetId, "tripMembers"));
            Assert.True(ReleaseSet(executor, session, "tripMembers").IsLogicalSuccess);
            Assert.False(IsMember(executor, session, bystanderId, "tripMembers"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static ProjectSequenceStepDto ActionStep(CommandAction action, Guid? stepId = null, Guid? nextStepId = null, bool isEnd = false)
    {
        var step = new ProjectSequenceStepDto
        {
            StepId = stepId ?? Guid.NewGuid(),
            BranchId = "main",
            StepType = SequenceStepType.Action,
            ContinuationMode = isEnd ? ContinuationMode.End : ContinuationMode.Continue,
            InlineAction = JsonExportService.ToCommandActionDtoForSequence(action, [])
        };
        if (nextStepId.HasValue) step.OnContinueStepId = nextStepId.Value;
        return step;
    }

    private static bool IsMember(RuntimeCommandActionExecutor executor, GameStateSession session, Guid memberId, string key)
    {
        var room = session.CurrentRoom!;
        var action = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "Check captured member",
            ActionType = CommandActionType.CheckObjectSetContainsObject,
            CheckObjectSetContainsObjectPayload = new RuntimeCheckObjectSetContainsObjectActionPayload(
                new ObjectSetReferenceDto { ObjectSetKey = key }, memberId.ToString("D"))
        };
        var outcome = executor.Execute(action,
            new RuntimeActionExecutionScope { RuntimeScopeNode = room, ScopeKind = room.ScopeKind, Name = room.Name, ScopeNameInGame = room.NameInGame },
            new RuntimeCommandProcessingRequest { Session = session }, new RuntimeActionInvocationContext(), [], [], allowBubbleUp: false);
        return outcome.IsLogicalSuccess;
    }

    private static RuntimeActionExecutionOutcome CaptureSet(RuntimeCommandActionExecutor executor, GameStateSession session, string key, Guid targetId, ObjectSetCaptureLifetime lifetime)
    {
        var room = session.CurrentRoom!;
        var action = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "Capture test set",
            ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
            CaptureObjectsOnObjectFootprintPayload = new RuntimeCaptureObjectsOnObjectFootprintActionPayload(
                new ObjectSetReferenceDto { ObjectSetKey = key }, targetId, FootprintMatchMode.AnyOverlap, lifetime)
        };
        return executor.Execute(action,
            new RuntimeActionExecutionScope { RuntimeScopeNode = room, ScopeKind = room.ScopeKind, Name = room.Name, ScopeNameInGame = room.NameInGame },
            new RuntimeCommandProcessingRequest { Session = session }, new RuntimeActionInvocationContext(), [], [], allowBubbleUp: false);
    }

    private static RuntimeActionExecutionOutcome ReleaseSet(RuntimeCommandActionExecutor executor, GameStateSession session, string key)
    {
        var room = session.CurrentRoom!;
        var action = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "Release test set",
            ActionType = CommandActionType.ReleaseObjectSet,
            ReleaseObjectSetPayload = new RuntimeReleaseObjectSetActionPayload(new ObjectSetReferenceDto { ObjectSetKey = key })
        };
        return executor.Execute(action,
            new RuntimeActionExecutionScope { RuntimeScopeNode = room, ScopeKind = room.ScopeKind, Name = room.Name, ScopeNameInGame = room.NameInGame },
            new RuntimeCommandProcessingRequest { Session = session }, new RuntimeActionInvocationContext(), [], [], allowBubbleUp: false);
    }

    private static IDisposable CreateRuntimeGameManager(object loader, GameCommandProcessorService processor, GameStateSession session, out IRuntimePendingEventQueue pendingEvents)
    {
        var managerType = typeof(GameStateSession).Assembly.GetType("Storyboard.Shared.GameManager.GameManager", throwOnError: true)!;
        var manager = Activator.CreateInstance(managerType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [loader, processor, null, null, null, null, null], null)!;
        managerType.GetMethod("AttachRoomTransitionPublication", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(manager, [session]);
        var field = managerType.GetField("_pendingEventQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        pendingEvents = Assert.IsAssignableFrom<IRuntimePendingEventQueue>(field!.GetValue(manager));
        return Assert.IsAssignableFrom<IDisposable>(manager);
    }

    private static System.Collections.IDictionary GetCapturedObjectSets(GameStateScopeNode scope)
    {
        var property = scope.GetType().GetProperty("CapturedObjectSets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<System.Collections.IDictionary>(property!.GetValue(scope));
    }

    private static GameObject Player(string name, Guid id, double x, double y) => Object(name, id, x, y);

    private static GameObject Object(string name, Guid id, double x, double y, int width = 1)
    {
        var variables = new List<GamePropertyDefinition>
        {
            NumberProperty("positionX", x),
            NumberProperty("positionY", y),
            NumberProperty("footprintWidthCells", width),
            NumberProperty("footprintHeightCells", 1)
        };
        if (name == "Player")
        {
            variables.Add(new GamePropertyDefinition { Name = "isPlayer", DefaultValue = "true", ValueRestriction = GamePropertyValueRestriction.TrueFalse });
        }

        return new GameObject
        {
            ObjectId = id,
            Name = name,
            PositionX = x,
            PositionY = y,
            FootprintWidthCells = width,
            Variables = variables
        };
    }

    private static GamePropertyDefinition NumberProperty(string name, double value) => new()
    {
        Name = name,
        DefaultValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ValueRestriction = GamePropertyValueRestriction.Numeric
    };
}
