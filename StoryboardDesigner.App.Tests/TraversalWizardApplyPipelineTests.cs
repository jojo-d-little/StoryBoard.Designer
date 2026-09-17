using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalWizardApplyPipelineTests
{
    [Fact]
    public void RunTraversalWizardForRoom_AddsTraversalDoorsAndLookActions_WithTogetherBinding()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var ui = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, ui);
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        var connection = Assert.Single(area.TraversalConnections);

        var sourceIsA = connection.RoomAId == source.Id;
        var sourceLeg = sourceIsA ? connection.TraversalStateFromA : connection.TraversalStateFromB;
        var destinationLeg = sourceIsA ? connection.TraversalStateFromB : connection.TraversalStateFromA;

        var sourceDoor = Assert.Single(source.GameObjects);
        var destinationDoor = Assert.Single(east.GameObjects);

        Assert.Equal("Door_E", sourceDoor.Name);
        Assert.Equal("Door_W", destinationDoor.Name);
        Assert.Equal("Door opens East from Source to East.", sourceDoor.ProducerNotes);
        Assert.Equal("Door opens West from East to Source.", destinationDoor.ProducerNotes);

        var sourceIsOpen = Assert.Single(sourceDoor.Variables, v => string.Equals(v.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        var sourceIsLocked = Assert.Single(sourceDoor.Variables, v => string.Equals(v.Name, "isLocked", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("false", sourceIsOpen.DefaultValue);
        Assert.Equal("true", sourceIsLocked.DefaultValue);

        Assert.Equal(sourceDoor.ObjectId, sourceLeg.OpenableObjectId);
        Assert.Equal(destinationDoor.ObjectId, destinationLeg.OpenableObjectId);

        var sourcePassable = Assert.Single(sourceLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        var destinationPassable = Assert.Single(destinationLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("false", sourcePassable.DefaultValue);
        Assert.Equal("false", destinationPassable.DefaultValue);

        Assert.Single(project.SharedVariables);
        var shared = project.SharedVariables[0];
        Assert.Equal("travel_between_Source_East", shared.Name);
        Assert.Equal(4, shared.Participants.Count);

        Assert.Contains(sourceLeg.AvailableActions,
            action => action.ActionType == CommandActionType.EchoMessage
                      && string.Equals(ActionPayloadAccessors.GetEchoMessage(action), "There looks to be a East thru the door", StringComparison.Ordinal));
        Assert.Contains(destinationLeg.AvailableActions,
            action => action.ActionType == CommandActionType.EchoMessage
                      && string.Equals(ActionPayloadAccessors.GetEchoMessage(action), "There looks to be a Source thru the door", StringComparison.Ordinal));

        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("TraversalWizard.Apply summary:", StringComparison.Ordinal)
                    && line.Contains("addedTraversals=1", StringComparison.Ordinal)
                    && line.Contains("createdDoors=2", StringComparison.Ordinal)
                    && line.Contains("skipped=0", StringComparison.Ordinal));

        Assert.Contains(ui.InformationMessages, message => message.Contains("added 1 traversal(s)", StringComparison.Ordinal));
    }

    [Fact]
    public void RunTraversalWizardForRoom_RecordsSkipReasons_ForExistingAndMissingNeighbor()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        area.TraversalConnections.Add(new TraversalConnection
        {
            RoomAId = source.Id,
            RoomBId = east.Id,
            BaseTraversalDirectionFromA = Direction10.East
        });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction is Direction.East or Direction.North,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        Assert.Single(area.TraversalConnections);

        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("TraversalWizard.Apply summary:", StringComparison.Ordinal)
                    && line.Contains("selected=2", StringComparison.Ordinal)
                    && line.Contains("addedTraversals=0", StringComparison.Ordinal)
                    && line.Contains("skipped=2", StringComparison.Ordinal));
        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("skip[existing-traversal]=1", StringComparison.Ordinal));
        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("skip[no-immediate-neighbor]=1", StringComparison.Ordinal));
    }

    [Fact]
    public void RunTraversalWizardForRoom_ReusesExistingDirectionalDoorNames()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };

        var existingSourceDoor = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Door_E",
            ProducerNotes = "existing-source"
        };

        var existingDestinationDoor = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Door_W",
            ProducerNotes = "existing-destination"
        };

        source.AddChildScope(existingSourceDoor);
        east.AddChildScope(existingDestinationDoor);

        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        var connection = Assert.Single(area.TraversalConnections);
        Assert.Single(source.GameObjects);
        Assert.Single(east.GameObjects);

        Assert.Same(existingSourceDoor, source.GameObjects[0]);
        Assert.Same(existingDestinationDoor, east.GameObjects[0]);

        var sourceIsA = connection.RoomAId == source.Id;
        var sourceLeg = sourceIsA ? connection.TraversalStateFromA : connection.TraversalStateFromB;
        var destinationLeg = sourceIsA ? connection.TraversalStateFromB : connection.TraversalStateFromA;

        Assert.Equal(existingSourceDoor.ObjectId, sourceLeg.OpenableObjectId);
        Assert.Equal(existingDestinationDoor.ObjectId, destinationLeg.OpenableObjectId);

        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("TraversalWizard.Apply summary:", StringComparison.Ordinal)
                    && line.Contains("createdDoors=0", StringComparison.Ordinal));
    }

    [Fact]
    public void RunTraversalWizardForRoom_CreatedDoors_PopulateLookingFromHereDefaults()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        Assert.True(viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode));

        var sourceDoor = Assert.Single(source.GameObjects);
        var destinationDoor = Assert.Single(east.GameObjects);

        var sourceLooking = Assert.Single(sourceDoor.Variables, variable => string.Equals(variable.Name, "lookingFromHere", StringComparison.OrdinalIgnoreCase));
        var destinationLooking = Assert.Single(destinationDoor.Variables, variable => string.Equals(variable.Name, "lookingFromHere", StringComparison.OrdinalIgnoreCase));
        var sourceDestinationFromHere = Assert.Single(sourceDoor.Variables, variable => string.Equals(variable.Name, "destinationFromHere", StringComparison.OrdinalIgnoreCase));
        var destinationDestinationFromHere = Assert.Single(destinationDoor.Variables, variable => string.Equals(variable.Name, "destinationFromHere", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("the east", sourceLooking.DefaultValue);
        Assert.Equal("the west", destinationLooking.DefaultValue);
        Assert.Equal("East", sourceDestinationFromHere.DefaultValue);
        Assert.Equal("Source", destinationDestinationFromHere.DefaultValue);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, sourceLooking.ValueRestriction);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, destinationLooking.ValueRestriction);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, sourceDestinationFromHere.ValueRestriction);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, destinationDestinationFromHere.ValueRestriction);
    }

    [Fact]
    public void RunTraversalWizardForRoom_ReusedDoors_FillsBlankLookingFromHereAndPreservesExistingText()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };

        var existingSourceDoor = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Door_E",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "lookingFromHere",
                    DefaultValue = "   ",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    Lifetime = GamePropertyLifetime.Singleton
                },
                new GamePropertyDefinition
                {
                    Name = "destinationFromHere",
                    DefaultValue = "   ",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    Lifetime = GamePropertyLifetime.Singleton
                }
            ]
        };

        var existingDestinationDoor = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Door_W",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "lookingFromHere",
                    DefaultValue = "toward the loading dock",
                    ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                    Lifetime = GamePropertyLifetime.Singleton
                },
                new GamePropertyDefinition
                {
                    Name = "destinationFromHere",
                    DefaultValue = "toward the courtyard",
                    ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                    Lifetime = GamePropertyLifetime.Singleton
                }
            ]
        };

        source.AddChildScope(existingSourceDoor);
        east.AddChildScope(existingDestinationDoor);

        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        Assert.True(viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode));

        var sourceDoor = Assert.Single(source.GameObjects);
        var destinationDoor = Assert.Single(east.GameObjects);
        Assert.Same(existingSourceDoor, sourceDoor);
        Assert.Same(existingDestinationDoor, destinationDoor);

        var sourceLooking = Assert.Single(sourceDoor.Variables, variable => string.Equals(variable.Name, "lookingFromHere", StringComparison.OrdinalIgnoreCase));
        var destinationLooking = Assert.Single(destinationDoor.Variables, variable => string.Equals(variable.Name, "lookingFromHere", StringComparison.OrdinalIgnoreCase));
        var sourceDestinationFromHere = Assert.Single(sourceDoor.Variables, variable => string.Equals(variable.Name, "destinationFromHere", StringComparison.OrdinalIgnoreCase));
        var destinationDestinationFromHere = Assert.Single(destinationDoor.Variables, variable => string.Equals(variable.Name, "destinationFromHere", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("the east", sourceLooking.DefaultValue);
        Assert.Equal("toward the loading dock", destinationLooking.DefaultValue);
        Assert.Equal("East", sourceDestinationFromHere.DefaultValue);
        Assert.Equal("toward the courtyard", destinationDestinationFromHere.DefaultValue);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, sourceLooking.ValueRestriction);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, destinationLooking.ValueRestriction);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, sourceDestinationFromHere.ValueRestriction);
        Assert.Equal(GamePropertyValueRestriction.Unrestricted, destinationDestinationFromHere.ValueRestriction);
    }

    [Fact]
    public void RunTraversalWizardForRoom_ReusedDoor_RemovesPriorIsOpenSharedParticipationBeforeRelink()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };

        var existingSourceDoor = new GameObject
        {
            Name = "Door_E",
            ProducerNotes = "existing-source"
        };

        var existingDestinationDoor = new GameObject
        {
            Name = "Door_W",
            ProducerNotes = "existing-destination"
        };

        source.AddChildScope(existingSourceDoor);
        east.AddChildScope(existingDestinationDoor);

        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        // Seed legacy participant entries to emulate previously-linked reused doors.
        var legacySharedId = Guid.NewGuid();
        project.SharedVariables.Add(new SharedVariableDefinition
        {
            Id = legacySharedId,
            Name = "legacy-door-open",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Participants =
            [
                new SharedVariableParticipant
                {
                    Kind = "object",
                    OwnerId = existingSourceDoor.ObjectId,
                    VariableName = "isOpen"
                },
                new SharedVariableParticipant
                {
                    Kind = "object",
                    OwnerId = existingDestinationDoor.ObjectId,
                    VariableName = "isOpen"
                }
            ]
        });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        var connection = Assert.Single(area.TraversalConnections);
        var sourceIsA = connection.RoomAId == source.Id;
        var sourceLeg = sourceIsA ? connection.TraversalStateFromA : connection.TraversalStateFromB;

        var activeSourceDoor = Assert.Single(source.GameObjects);
        var activeDestinationDoor = Assert.Single(east.GameObjects);
        Assert.Same(existingSourceDoor, activeSourceDoor);
        Assert.Same(existingDestinationDoor, activeDestinationDoor);

        var sourceIsOpen = Assert.Single(activeSourceDoor.Variables, v => string.Equals(v.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        var destinationIsOpen = Assert.Single(activeDestinationDoor.Variables, v => string.Equals(v.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(sourceIsOpen.SharedVariableId);
        Assert.Equal(sourceIsOpen.SharedVariableId, destinationIsOpen.SharedVariableId);
        Assert.NotEqual(legacySharedId, sourceIsOpen.SharedVariableId.Value);

        var legacyShared = project.SharedVariables.FirstOrDefault(shared => shared.Id == legacySharedId);
        Assert.True(legacyShared is null || !legacyShared.Participants.Any(participant =>
            string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase)
            && string.Equals(participant.VariableName, "isOpen", StringComparison.OrdinalIgnoreCase)
            && (participant.OwnerId == activeSourceDoor.ObjectId || participant.OwnerId == activeDestinationDoor.ObjectId)));

        Assert.Equal(sourceIsOpen.SharedVariableId, sourceLeg.SharedVariableId);
    }

    [Fact]
    public void RunTraversalWizardForRoom_CompletesUnderOneSecond_ForSingleRoomEightNeighborScope()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Center" };
        area.Rooms.Add(source);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 2), Y = 24 + (140 * 2) });

        AddNeighbor(area, source, "North", dx: 0, dy: -1);
        AddNeighbor(area, source, "NorthEast", dx: 1, dy: -1);
        AddNeighbor(area, source, "East", dx: 1, dy: 0);
        AddNeighbor(area, source, "SouthEast", dx: 1, dy: 1);
        AddNeighbor(area, source, "South", dx: 0, dy: 1);
        AddNeighbor(area, source, "SouthWest", dx: -1, dy: 1);
        AddNeighbor(area, source, "West", dx: -1, dy: 0);
        AddNeighbor(area, source, "NorthWest", dx: -1, dy: -1);

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.HasImmediateNeighbor && !seed.HasExistingTraversal,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Center");

        var stopwatch = Stopwatch.StartNew();
        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);
        stopwatch.Stop();

        Assert.True(handled);
        Assert.Equal(8, area.TraversalConnections.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Traversal wizard apply exceeded 1 second: {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void RunTraversalWizardForRoom_PerSideDoorMode_CreatesIndependentSharedPools()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.PerSide,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        var connection = Assert.Single(area.TraversalConnections);
        Assert.Equal(OpenStateBindingMode.Independent, connection.OpenStateBindingMode);

        Assert.Equal(2, project.SharedVariables.Count);
        Assert.All(project.SharedVariables, shared => Assert.Equal(2, shared.Participants.Count));
        Assert.Contains(project.SharedVariables, shared => string.Equals(shared.Name, "travel_from_Source_to_East", StringComparison.Ordinal));
        Assert.Contains(project.SharedVariables, shared => string.Equals(shared.Name, "travel_from_East_to_Source", StringComparison.Ordinal));

        var sourceDoor = Assert.Single(source.GameObjects);
        var destinationDoor = Assert.Single(east.GameObjects);
        var sourceIsA = connection.RoomAId == source.Id;
        var sourceLeg = sourceIsA ? connection.TraversalStateFromA : connection.TraversalStateFromB;
        var destinationLeg = sourceIsA ? connection.TraversalStateFromB : connection.TraversalStateFromA;

        var sharedById = project.SharedVariables.ToDictionary(shared => shared.Id);

        Assert.Contains(sourceLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(destinationLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));

        var sourcePassable = Assert.Single(sourceLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        var destinationPassable = Assert.Single(destinationLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        var sourceIsOpen = Assert.Single(sourceDoor.Variables, v => string.Equals(v.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        var destinationIsOpen = Assert.Single(destinationDoor.Variables, v => string.Equals(v.Name, "isOpen", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(sourcePassable.SharedVariableId);
        Assert.NotNull(sourceIsOpen.SharedVariableId);
        Assert.Equal(sourcePassable.SharedVariableId, sourceIsOpen.SharedVariableId);

        Assert.NotNull(destinationPassable.SharedVariableId);
        Assert.NotNull(destinationIsOpen.SharedVariableId);
        Assert.Equal(destinationPassable.SharedVariableId, destinationIsOpen.SharedVariableId);

        Assert.NotEqual(sourcePassable.SharedVariableId, destinationPassable.SharedVariableId);
        Assert.Contains(sourcePassable.SharedVariableId!.Value, sharedById.Keys);
        Assert.Contains(destinationPassable.SharedVariableId!.Value, sharedById.Keys);
    }

    [Fact]
    public void RunTraversalWizardForRoom_DoorDisabled_AddsTraversalOnlyWithoutDoorObjects()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = false,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Open,
                    DoorLockedWhenClosed = false,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        var connection = Assert.Single(area.TraversalConnections);
        Assert.Empty(project.SharedVariables);
        Assert.Empty(source.GameObjects);
        Assert.Empty(east.GameObjects);

        var sourceIsA = connection.RoomAId == source.Id;
        var sourceLeg = sourceIsA ? connection.TraversalStateFromA : connection.TraversalStateFromB;
        var destinationLeg = sourceIsA ? connection.TraversalStateFromB : connection.TraversalStateFromA;

        Assert.Null(sourceLeg.OpenableObjectId);
        Assert.Null(destinationLeg.OpenableObjectId);

        var sourcePassable = Assert.Single(sourceLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        var destinationPassable = Assert.Single(destinationLeg.Variables, v => string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("true", sourcePassable.DefaultValue);
        Assert.Equal("true", destinationPassable.DefaultValue);
        Assert.Null(sourcePassable.SharedVariableId);
        Assert.Null(destinationPassable.SharedVariableId);

        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("TraversalWizard.Apply summary:", StringComparison.Ordinal)
                    && line.Contains("createdDoors=0", StringComparison.Ordinal)
                    && line.Contains("addedTraversals=1", StringComparison.Ordinal));
    }

    [Fact]
    public void RunTraversalWizardForRoom_UsesSelectedDoorTemplate_WhenProvided()
    {
        var project = CreateProjectWithSingleArea();
        project.RoomImageCanvasWidth = 1000;
        project.RoomImageCanvasHeight = 700;
        var area = project.Planets[0].Countries[0].Areas[0];

        var selectedTemplateId = Guid.NewGuid();
        project.ObjectTemplates.Add(new GameObject
        {
            ObjectId = selectedTemplateId,
            Name = "Iron Door Template",
            Description = "A heavy iron door.",
            IsContainer = true,
            ContainerPointsDefaultValue = 3,
            Commands = new List<string> { "inspect" },
            ContainedObjects =
            [
                new GameObject
                {
                    Name = "Door Plaque",
                    Description = "Small brass plaque."
                }
            ]
        });

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    DoorTemplateId = selectedTemplateId,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        Assert.Single(area.TraversalConnections);

        var sourceDoor = Assert.Single(source.GameObjects);
        var destinationDoor = Assert.Single(east.GameObjects);

        Assert.Equal("A heavy iron door.", sourceDoor.Description);
        Assert.Equal("A heavy iron door.", destinationDoor.Description);
        Assert.True(sourceDoor.IsContainer);
        Assert.True(destinationDoor.IsContainer);
        Assert.Equal(3, sourceDoor.ContainerPointsDefaultValue);
        Assert.Equal(3, destinationDoor.ContainerPointsDefaultValue);
        Assert.Single(sourceDoor.ContainedObjects);
        Assert.Single(destinationDoor.ContainedObjects);
        Assert.Equal("Door Plaque", sourceDoor.ContainedObjects[0].Name);
        Assert.Equal("Door Plaque", destinationDoor.ContainedObjects[0].Name);

        Assert.True(sourceDoor.IsOpenable);
        Assert.True(destinationDoor.IsOpenable);
        Assert.True(sourceDoor.IsLockable);
        Assert.True(destinationDoor.IsLockable);
        Assert.False(sourceDoor.IsInventoriable);
        Assert.False(destinationDoor.IsInventoriable);

        Assert.Equal(90, sourceDoor.ImageRotationDegrees);
        Assert.Equal(270, destinationDoor.ImageRotationDegrees);
        Assert.Equal(704, sourceDoor.PositionX);
        Assert.Equal(252, sourceDoor.PositionY);
        Assert.Equal(0, destinationDoor.PositionX);
        Assert.Equal(252, destinationDoor.PositionY);
    }

    [Fact]
    public void RunTraversalWizardForRoom_DoorTemplateChoices_OnlyIncludeDoorNamedTemplates()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var ironDoorTemplateId = Guid.NewGuid();
        var woodDoorTemplateId = Guid.NewGuid();
        project.ObjectTemplates.Add(new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Treasure Chest"
        });
        project.ObjectTemplates.Add(new GameObject
        {
            ObjectId = ironDoorTemplateId,
            Name = "Iron Door"
        });
        project.ObjectTemplates.Add(new GameObject
        {
            ObjectId = woodDoorTemplateId,
            Name = "Wood door"
        });

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        TraversalWizardDialogRequest? capturedRequest = null;
        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request =>
            {
                capturedRequest = request;
                return TraversalWizardDialogResult.Empty;
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var handled = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handled);
        var request = Assert.IsType<TraversalWizardDialogRequest>(capturedRequest);
        Assert.Equal(2, request.DoorTemplateOptions.Count);
        Assert.Equal("Iron Door", request.DoorTemplateOptions[0].DisplayName);
        Assert.Equal("Wood door", request.DoorTemplateOptions[1].DisplayName);

        var eastSeed = Assert.Single(request.Rows, row => row.Direction == Direction.East);
        Assert.Equal(ironDoorTemplateId, eastSeed.DefaultDoorTemplateId);
    }

    [Fact]
    public void RunTraversalWizardForRoom_FourDirectional_DiagonalRequiresExplicitSelection()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];
        area.AdjacencyMode = AreaAdjacencyMode.FourDirectional;

        var source = new Room { Name = "Source" };
        var northEast = new Room { Name = "NorthEast" };
        area.Rooms.Add(source);
        area.Rooms.Add(northEast);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = northEast.Id, X = 24 + (180 * 2), Y = 24 + (140 * 0) });

        var sourceNode = FindRoomNode(project, "Source");

        var noSelectionContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = false,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };
        var noSelectionVm = MainWindowViewModelTestHarness.CreateViewModel(project, noSelectionContext, new ProjectUiServiceStub());

        var handledNoSelection = noSelectionVm.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handledNoSelection);
        Assert.Empty(area.TraversalConnections);

        var diagonalSelectionContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = seed.HasExistingTraversal,
                    IncludeTraversal = seed.Direction == Direction.NorthEast,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };
        var diagonalVm = MainWindowViewModelTestHarness.CreateViewModel(project, diagonalSelectionContext, new ProjectUiServiceStub());

        var handledDiagonalSelection = diagonalVm.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(handledDiagonalSelection);
        var connection = Assert.Single(area.TraversalConnections);
        var directionFromSource = connection.RoomAId == source.Id
            ? connection.BaseTraversalDirectionFromA
            : TraversalWizardApplyUtility.OppositeDirection(connection.BaseTraversalDirectionFromA);
        Assert.Equal(Direction10.NorthEast, directionFromSource);
    }

    [Fact]
    public void RunTraversalWizardForRoom_ReRun_IsIdempotentByPairAndReportsDuplicateSkip()
    {
        var project = CreateProjectWithSingleArea();
        var area = project.Planets[0].Countries[0].Areas[0];

        var source = new Room { Name = "Source" };
        var east = new Room { Name = "East" };
        area.Rooms.Add(source);
        area.Rooms.Add(east);
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = source.Id, X = 24 + (180 * 1), Y = 24 + (140 * 1) });
        area.RoomPlacements.Add(new AreaRoomPlacement { RoomId = east.Id, X = 24 + (180 * 2), Y = 24 + (140 * 1) });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ReviewHandler = request => new TraversalWizardDialogResult
            {
                Rows = request.Rows.Select(seed => new TraversalWizardDirectionChoice
                {
                    Direction = seed.Direction,
                    HasImmediateNeighbor = seed.HasImmediateNeighbor,
                    HasExistingTraversal = false,
                    IncludeTraversal = seed.Direction == Direction.East,
                    DoorEnabled = true,
                    DoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                    DoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DoorLockedWhenClosed = true,
                    StatusText = seed.StatusText
                }).ToList()
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var sourceNode = FindRoomNode(project, "Source");

        var firstRun = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);
        Assert.True(firstRun);
        Assert.Single(area.TraversalConnections);

        var secondRun = viewModel.ExecuteTreeContextAction("run-traversal-wizard", sourceNode);

        Assert.True(secondRun);
        Assert.Single(area.TraversalConnections);
        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("TraversalWizard.Apply summary:", StringComparison.Ordinal)
                    && line.Contains("addedTraversals=0", StringComparison.Ordinal)
                    && line.Contains("skipped=1", StringComparison.Ordinal));
        Assert.Contains(viewModel.OutputConsoleLines,
            line => line.Contains("skip[duplicate-pair]=1", StringComparison.Ordinal));
    }

    private static RoomNodeViewModel FindRoomNode(ProjectModel project, string roomName)
    {
        var buildHierarchyMethod = typeof(MainWindowViewModel).GetMethod("BuildHierarchy", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(buildHierarchyMethod);

        var hierarchy = Assert.IsType<List<HierarchyNodeViewModel>>(buildHierarchyMethod!.Invoke(null, [project]));
        var roomNode = EnumerateNodes(hierarchy)
            .OfType<RoomNodeViewModel>()
            .Single(node => string.Equals(node.Room.Name, roomName, StringComparison.Ordinal));

        return roomNode;
    }

    private static IEnumerable<HierarchyNodeViewModel> EnumerateNodes(IEnumerable<HierarchyNodeViewModel> roots)
    {
        var stack = new Stack<HierarchyNodeViewModel>(roots.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private static ProjectModel CreateProjectWithSingleArea()
    {
        var area = new Area { Name = "Area" };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };

        return new ProjectModel
        {
            Name = "Traversal Wizard Test",
            Planets = [planet],
            GlobalObjectScopeName = "Player"
        };
    }

    private static void AddNeighbor(Area area, Room source, string name, int dx, int dy)
    {
        var room = new Room { Name = name };
        area.Rooms.Add(room);

        var sourcePlacement = area.RoomPlacements.Single(placement => placement.RoomId == source.Id);
        area.RoomPlacements.Add(new AreaRoomPlacement
        {
            RoomId = room.Id,
            X = sourcePlacement.X + (dx * 180),
            Y = sourcePlacement.Y + (dy * 140)
        });
    }

}
