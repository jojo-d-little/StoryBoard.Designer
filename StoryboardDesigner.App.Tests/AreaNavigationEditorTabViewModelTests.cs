using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class AreaNavigationEditorTabViewModelTests
{
    [Fact]
    public void AddTraversalConnection_UsesSelectedSourceRoom_WhenAvailable()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        var roomC = new Room { Name = "C" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB, roomC },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24 },
                new() { RoomId = roomB.Id, X = 204, Y = 24 },
                new() { RoomId = roomC.Id, X = 24, Y = 164 }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        vm.SetTraversalSourceRoom(roomC.Id);

        vm.AddTraversalConnection();

        var connection = Assert.Single(vm.TraversalConnections);
        Assert.Contains(connection.RoomAId, new[] { roomA.Id, roomC.Id });
        Assert.Contains(connection.RoomBId, new[] { roomA.Id, roomC.Id });
        Assert.True(connection.RoomAId != connection.RoomBId);

        var directionFromSelectedToOther = connection.RoomAId == roomC.Id
            ? connection.BaseTraversalDirectionFromA
            : InvertDirection(connection.BaseTraversalDirectionFromA);

        Assert.Equal(Direction10.North, directionFromSelectedToOther);
        Assert.Contains("C", vm.SelectedTraversalSourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveRoomFromMap_ClearsSelectedTraversalSource_WhenSelectedRoomRemoved()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24 },
                new() { RoomId = roomB.Id, X = 204, Y = 24 }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        vm.SetTraversalSourceRoom(roomA.Id);

        vm.RemoveRoomFromMap(roomA);

        Assert.Null(vm.SelectedTraversalSourceRoomId);
        Assert.Equal("Traversal source: Auto nearest", vm.SelectedTraversalSourceText);
    }

    [Fact]
    public void PlaceRoom_WithAutoTraversalDisabled_DoesNotCreateAdjacentTraversal()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24 }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);

        var placed = vm.PlaceRoom(roomB, 204, 24, out var validationMessage, createAutoTraversals: false);

        Assert.True(placed);
        Assert.Null(validationMessage);
        Assert.Empty(vm.TraversalConnections);
        Assert.Empty(area.TraversalConnections);
    }

    [Fact]
    public void CancelPlacementDrag_RestoresOriginalPosition_AndKeepsTraversals()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24 },
                new() { RoomId = roomB.Id, X = 204, Y = 24 }
            },
            TraversalConnections = new List<TraversalConnection>
            {
                new()
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    BaseTraversalDirectionFromA = Direction10.East,
                    TraversalAccessMode = TraversalAccessMode.TwoWay
                }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        var placement = Assert.Single(vm.RoomPlacements, p => p.Room.Id == roomA.Id);
        var originalX = placement.X;
        var originalY = placement.Y;

        vm.BeginPlacementDrag(placement);
        Assert.True(vm.TryPreviewPlacement(placement, 24, 24 + 140));

        vm.CancelPlacementDrag(placement);

        var restoredPlacement = Assert.Single(vm.RoomPlacements, p => p.Room.Id == roomA.Id);
        Assert.Equal(originalX, restoredPlacement.X);
        Assert.Equal(originalY, restoredPlacement.Y);
        Assert.Single(vm.TraversalConnections);
    }

    [Fact]
    public void CommitPlacementDrag_WithConnectedTraversals_DeletesTraversals_AndUndoRestoresState()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24 },
                new() { RoomId = roomB.Id, X = 204, Y = 24 }
            },
            TraversalConnections = new List<TraversalConnection>
            {
                new()
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    BaseTraversalDirectionFromA = Direction10.East,
                    TraversalAccessMode = TraversalAccessMode.TwoWay
                }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        var placement = Assert.Single(vm.RoomPlacements, p => p.Room.Id == roomA.Id);
        var originalX = placement.X;
        var originalY = placement.Y;

        vm.BeginPlacementDrag(placement);
        Assert.True(vm.TryPreviewPlacement(placement, 24, 24 + 140));

        var committed = vm.CommitPlacementDrag(placement, deleteConnectedTraversals: true, out var deletedCount);

        Assert.True(committed);
        Assert.Equal(1, deletedCount);
        Assert.Empty(vm.TraversalConnections);
        var movedPlacement = Assert.Single(vm.RoomPlacements, p => p.Room.Id == roomA.Id);
        Assert.Equal(originalX, movedPlacement.X);
        Assert.NotEqual(originalY, movedPlacement.Y);

        Assert.True(vm.TryUndoLastAction());
        Assert.Single(vm.TraversalConnections);
        var restoredPlacement = Assert.Single(vm.RoomPlacements, p => p.Room.Id == roomA.Id);
        Assert.Equal(originalX, restoredPlacement.X);
        Assert.Equal(originalY, restoredPlacement.Y);
    }

    [Fact]
    public void SetTraversalValidationIssues_HighlightsArrowsAndBuildsToolTip()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        var connection = new TraversalConnection
        {
            TraversalConnectionId = Guid.NewGuid(),
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East,
            TraversalAccessMode = TraversalAccessMode.TwoWay
        };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24 },
                new() { RoomId = roomB.Id, X = 204, Y = 24 }
            },
            TraversalConnections = new List<TraversalConnection> { connection }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        vm.SetTraversalValidationIssues(
        [
            new ProjectValidationIssue(
                ValidationSeverity.Error,
                $"Global / Planet / Country / Area / TraversalConnection/{connection.TraversalConnectionId:N}",
                "traversal leg is missing required isPassable variable.",
                RuleId: "TRV-003")
        ]);

        Assert.Equal(2, vm.NavigationArrows.Count);
        Assert.All(vm.NavigationArrows, arrow => Assert.True(arrow.HasValidationError));
        Assert.All(vm.NavigationArrows, arrow => Assert.Contains("TRV-003", arrow.ValidationToolTip, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetTraversalValidationIssues_IgnoresIssuesWithoutTraversalConnectionPath()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        var connection = new TraversalConnection
        {
            TraversalConnectionId = Guid.NewGuid(),
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East,
            TraversalAccessMode = TraversalAccessMode.TwoWay
        };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24 },
                new() { RoomId = roomB.Id, X = 204, Y = 24 }
            },
            TraversalConnections = new List<TraversalConnection> { connection }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        vm.SetTraversalValidationIssues(
        [
            new ProjectValidationIssue(
                ValidationSeverity.Error,
                "Global / Planet / Country / Area",
                "area-level issue",
                RuleId: "PROJ-001")
        ]);

        Assert.Equal(2, vm.NavigationArrows.Count);
        Assert.All(vm.NavigationArrows, arrow => Assert.False(arrow.HasValidationError));
    }

    [Fact]
    public void SetSelectedFloorElevation_FiltersVisibleRoomPlacements()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24, FloorElevation = 0 },
                new() { RoomId = roomB.Id, X = 24, Y = 24, FloorElevation = 1 }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);

        Assert.Equal(0, vm.SelectedFloorElevation);
        Assert.Single(vm.VisibleRoomPlacements);
        Assert.Equal(roomA.Id, vm.VisibleRoomPlacements[0].Room.Id);

        vm.SetSelectedFloorElevation(1);

        Assert.Equal(1, vm.SelectedFloorElevation);
        Assert.Single(vm.VisibleRoomPlacements);
        Assert.Equal(roomB.Id, vm.VisibleRoomPlacements[0].Room.Id);
    }

    [Fact]
    public void PlaceRoom_AllowsSameGridCell_WhenOccupiedOnDifferentFloor()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24, FloorElevation = 0 }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        vm.SetSelectedFloorElevation(1);

        var placed = vm.PlaceRoom(roomB, 24, 24, out var validationMessage, createAutoTraversals: false);

        Assert.True(placed);
        Assert.Null(validationMessage);
        Assert.Equal(2, vm.RoomPlacements.Count);
        Assert.Single(vm.VisibleRoomPlacements);

        var roomBPlacement = vm.RoomPlacements.Single(p => p.Room.Id == roomB.Id);
        Assert.Equal(1, roomBPlacement.FloorElevation);
    }

    [Fact]
    public void SetSelectedFloorElevation_RebuildsNavigationArrows_ForVisibleFloorOnly()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        var roomC = new Room { Name = "C" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomA, roomB, roomC },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomA.Id, X = 24, Y = 24, FloorElevation = 0 },
                new() { RoomId = roomB.Id, X = 204, Y = 24, FloorElevation = 0 },
                new() { RoomId = roomC.Id, X = 24, Y = 24, FloorElevation = 1 }
            },
            TraversalConnections = new List<TraversalConnection>
            {
                new()
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    BaseTraversalDirectionFromA = Direction10.East,
                    TraversalAccessMode = TraversalAccessMode.TwoWay
                },
                new()
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomC.Id,
                    BaseTraversalDirectionFromA = Direction10.North,
                    TraversalAccessMode = TraversalAccessMode.TwoWay
                }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);

        Assert.Equal(2, vm.NavigationArrows.Count);

        vm.SetSelectedFloorElevation(1);

        Assert.Empty(vm.NavigationArrows);
    }

    [Fact]
    public void VerticalIndicators_ShowRoomAndTraversalSignals_IndependentlyPerDirection()
    {
        var roomGround = new Room { Name = "Ground" };
        var roomUpper = new Room { Name = "Upper" };
        var roomLower = new Room { Name = "Lower" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomGround, roomUpper, roomLower },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomGround.Id, X = 24, Y = 24, FloorElevation = 0 },
                new() { RoomId = roomUpper.Id, X = 24, Y = 24, FloorElevation = 1 },
                new() { RoomId = roomLower.Id, X = 24, Y = 24, FloorElevation = -1 }
            },
            TraversalConnections = new List<TraversalConnection>
            {
                new()
                {
                    RoomAId = roomGround.Id,
                    RoomBId = roomUpper.Id,
                    BaseTraversalDirectionFromA = Direction10.North,
                    TraversalAccessMode = TraversalAccessMode.OneWayAtoB
                }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);

        var groundPlacement = vm.RoomPlacements.Single(p => p.Room.Id == roomGround.Id);
        Assert.True(groundPlacement.HasRoomAbove);
        Assert.True(groundPlacement.HasRoomBelow);
        Assert.True(groundPlacement.HasTraversalUp);
        Assert.False(groundPlacement.HasTraversalDown);

        var upperPlacement = vm.RoomPlacements.Single(p => p.Room.Id == roomUpper.Id);
        Assert.True(upperPlacement.HasRoomBelow);
        Assert.False(upperPlacement.HasRoomAbove);
        Assert.False(upperPlacement.HasTraversalDown);
        Assert.False(upperPlacement.HasTraversalUp);

        var lowerPlacement = vm.RoomPlacements.Single(p => p.Room.Id == roomLower.Id);
        Assert.True(lowerPlacement.HasRoomAbove);
        Assert.False(lowerPlacement.HasRoomBelow);
        Assert.False(lowerPlacement.HasTraversalUp);
        Assert.False(lowerPlacement.HasTraversalDown);
    }

    [Fact]
    public void VerticalIndicators_UpdateAfterRemovingConnectedRoom()
    {
        var roomGround = new Room { Name = "Ground" };
        var roomUpper = new Room { Name = "Upper" };

        var area = new Area
        {
            Name = "Area",
            Rooms = new List<Room> { roomGround, roomUpper },
            RoomPlacements = new List<AreaRoomPlacement>
            {
                new() { RoomId = roomGround.Id, X = 24, Y = 24, FloorElevation = 0 },
                new() { RoomId = roomUpper.Id, X = 24, Y = 24, FloorElevation = 1 }
            },
            TraversalConnections = new List<TraversalConnection>
            {
                new()
                {
                    RoomAId = roomGround.Id,
                    RoomBId = roomUpper.Id,
                    BaseTraversalDirectionFromA = Direction10.North,
                    TraversalAccessMode = TraversalAccessMode.TwoWay
                }
            }
        };

        var vm = new AreaNavigationEditorTabViewModel(area);
        var groundPlacement = vm.RoomPlacements.Single(p => p.Room.Id == roomGround.Id);
        Assert.True(groundPlacement.HasRoomAbove);
        Assert.True(groundPlacement.HasTraversalUp);

        vm.RemoveRoomFromMap(roomUpper);

        Assert.False(groundPlacement.HasRoomAbove);
        Assert.False(groundPlacement.HasTraversalUp);
    }

    [Fact]
    public void CloneCommandAction_PreservesOutcomeMessageMapEntries()
    {
        var source = new CommandAction
        {
            Name = "Look",
            ActionType = CommandActionType.EchoMessage,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "You see a room.",
                ["Failure"] = string.Empty
            }
        };

        var cloneMethod = typeof(AreaNavigationEditorTabViewModel).GetMethod(
            "CloneCommandAction",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(cloneMethod);

        var cloned = Assert.IsType<CommandAction>(cloneMethod!.Invoke(null, [source]));

        Assert.True(cloned.OutcomeMessageMap.ContainsKey("Success"));
        Assert.Equal("You see a room.", cloned.OutcomeMessageMap["Success"]);
    }

    private static Direction10 InvertDirection(Direction10 direction)
    {
        return direction switch
        {
            Direction10.North => Direction10.South,
            Direction10.NorthEast => Direction10.SouthWest,
            Direction10.East => Direction10.West,
            Direction10.SouthEast => Direction10.NorthWest,
            Direction10.South => Direction10.North,
            Direction10.SouthWest => Direction10.NorthEast,
            Direction10.West => Direction10.East,
            Direction10.NorthWest => Direction10.SouthEast,
            Direction10.Up => Direction10.Down,
            Direction10.Down => Direction10.Up,
            _ => Direction10.North
        };
    }
}

