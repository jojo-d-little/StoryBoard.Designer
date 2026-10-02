using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class AreaNavigationEditorTabViewModelTests
{
    [Fact]
    public void ResizeAndDrag_RejectEntireFootprintOverlapAndGridOverflow_AndUndoRestoresSpan()
    {
        var largeRoom = new Room { Name = "Large" };
        var neighbor = new Room { Name = "Neighbor" };
        var edgeRoom = new Room { Name = "Edge" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [largeRoom, neighbor, edgeRoom],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = largeRoom.Id, X = 24, Y = 24 },
                new AreaRoomPlacement { RoomId = neighbor.Id, X = 24 + 3 * 180, Y = 24 },
                new AreaRoomPlacement { RoomId = edgeRoom.Id, X = 24 + 6 * 180, Y = 24 + 4 * 140 }
            ]
        };
        var editor = new AreaNavigationEditorTabViewModel(area);
        var large = editor.RoomPlacements.Single(p => p.Room.Id == largeRoom.Id);

        Assert.True(editor.TryResizePlacement(large, 3, 3, out _));
        Assert.Equal(488, large.CardWidth);
        Assert.Equal(376, large.CardHeight);
        Assert.False(editor.PlaceRoom(neighbor, 24 + 2 * 180, 24 + 2 * 140,
            out var overlapMessage, createAutoTraversals: false));
        Assert.Contains("overlaps", overlapMessage);

        editor.BeginPlacementDrag(large);
        Assert.False(editor.TryPreviewPlacement(large, 24 + 180, 24));
        editor.CancelPlacementDrag(large);
        Assert.True(editor.TryUndoLastAction());
        Assert.Equal(1, editor.RoomPlacements.Single(p => p.Room.Id == largeRoom.Id).ColumnSpan);

        var edge = editor.RoomPlacements.Single(p => p.Room.Id == edgeRoom.Id);
        Assert.False(editor.TryResizePlacement(edge, 3, 3, out var edgeMessage));
        Assert.Contains("beyond the grid", edgeMessage);
    }

    [Fact]
    public void LoadedFootprintsExpandGridAndOverlappingDifferentFloorsShowVerticalIndicators()
    {
        var ground = new Room { Name = "Ground" };
        var upper = new Room { Name = "Upper" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [ground, upper],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = ground.Id, X = 24 + 7 * 180, Y = 24 + 5 * 140,
                    ColumnSpan = 3, RowSpan = 3 },
                new AreaRoomPlacement { RoomId = upper.Id, X = 24 + 8 * 180, Y = 24 + 6 * 140,
                    FloorElevation = 1 }
            ]
        };

        var editor = new AreaNavigationEditorTabViewModel(area);

        Assert.Equal(10, editor.GridColumns);
        Assert.Equal(8, editor.GridRows);
        Assert.True(editor.RoomPlacements.Single(p => p.Room.Id == ground.Id).HasRoomAbove);
        Assert.True(editor.RoomPlacements.Single(p => p.Room.Id == upper.Id).HasRoomBelow);
    }

    [Fact]
    public void ThreeSouthNeighborArrowsOccupyDistinctPositionsAlongLargeRoomBoundary()
    {
        var large = new Room { Name = "Large" };
        var neighbors = Enumerable.Range(0, 3).Select(i => new Room { Name = $"South {i}" }).ToArray();
        var area = new Area
        {
            Name = "Area",
            Rooms = [large, ..neighbors],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = large.Id, X = 24, Y = 24, ColumnSpan = 3, RowSpan = 3 },
                ..neighbors.Select((room, i) => new AreaRoomPlacement
                    { RoomId = room.Id, X = 24 + i * 180, Y = 24 + 3 * 140 })
            ],
            TraversalConnections = neighbors.Select(room => new TraversalConnection
            {
                RoomAId = large.Id,
                RoomBId = room.Id,
                BaseTraversalDirectionFromA = Direction10.South,
                TraversalAccessMode = TraversalAccessMode.TwoWay
            }).ToList()
        };

        var editor = new AreaNavigationEditorTabViewModel(area);
        var outbound = editor.NavigationArrows.Where(a => a.SourceRoomId == large.Id).ToArray();

        Assert.Equal(3, outbound.Length);
        Assert.Equal(3, outbound.Select(a => Math.Round(a.X)).Distinct().Count());
        Assert.All(outbound, arrow => Assert.InRange(arrow.Y, 390, 440));
        Assert.All(outbound, arrow => Assert.Equal(90, arrow.Angle, 6));
        Assert.All(editor.NavigationArrows.Where(a => a.DestinationRoomId == large.Id),
            arrow => Assert.Equal(-90, arrow.Angle, 6));
    }

    [Fact]
    public void FoyerAndHallwayArrowsAlignWithTheirSharedNorthSouthEdge()
    {
        var foyer = new Room { Name = "Elevator Foyer" };
        var hallway = new Room { Name = "North Hallway" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [foyer, hallway],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = foyer.Id, X = 204, Y = 164, ColumnSpan = 2, FloorElevation = 2 },
                new AreaRoomPlacement { RoomId = hallway.Id, X = 384, Y = 304, FloorElevation = 2 }
            ],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = foyer.Id,
                    RoomBId = hallway.Id,
                    BaseTraversalDirectionFromA = Direction10.South,
                    TraversalAccessMode = TraversalAccessMode.TwoWay
                }
            ]
        };

        var editor = new AreaNavigationEditorTabViewModel(area);
        editor.SetSelectedFloorElevation(2);
        var south = Assert.Single(editor.NavigationArrows.Where(arrow => arrow.SourceRoomId == foyer.Id));
        var north = Assert.Single(editor.NavigationArrows.Where(arrow => arrow.SourceRoomId == hallway.Id));

        Assert.Equal(90, south.Angle, 6);
        Assert.Equal(-90, north.Angle, 6);
        Assert.Equal(438, south.X, 6);
        Assert.Equal(458, north.X, 6);
        Assert.Equal(282, south.Y, 6);
        Assert.Equal(282, north.Y, 6);
    }

    [Fact]
    public void AutoTraversals_LeaveSharedLargeRoomSideForPairEditing()
    {
        var large = new Room { Name = "Large" };
        var southA = new Room { Name = "South A" };
        var southB = new Room { Name = "South B" };
        var southC = new Room { Name = "South C" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [large, southA, southB, southC],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = large.Id, X = 24, Y = 24, ColumnSpan = 3, RowSpan = 3 },
                new AreaRoomPlacement { RoomId = southA.Id, X = 24, Y = 444 },
                new AreaRoomPlacement { RoomId = southB.Id, X = 204, Y = 444 },
                new AreaRoomPlacement { RoomId = southC.Id, X = 384, Y = 444 }
            ]
        };

        var editor = new AreaNavigationEditorTabViewModel(area);
        Assert.True(editor.PlaceRoom(large, 24, 24, out var notice, createAutoTraversals: true));
        Assert.Contains("Manage Traversals", notice);
        Assert.Empty(editor.TraversalConnections);

        var pairA = new TraversalConnection
        {
            RoomAId = large.Id, RoomBId = southA.Id,
            BaseTraversalDirectionFromA = Direction10.South,
            TraversalAccessMode = TraversalAccessMode.TwoWay
        };
        area.TraversalConnections.Add(pairA);
        editor.TraversalConnections.Add(pairA);

        Assert.True(editor.PlaceRoom(southB, 204, 444, out notice, createAutoTraversals: true));
        Assert.Contains("Manage Traversals", notice);
        Assert.Single(editor.TraversalConnections.Where(connection =>
            connection.RoomAId == large.Id || connection.RoomBId == large.Id));
    }

    [Fact]
    public void AutoTraversals_CreateOneUnambiguousLargeRoomPair_ThenStopAtSecondSouthNeighbor()
    {
        var large = new Room { Name = "Large" };
        var southA = new Room { Name = "South A" };
        var southB = new Room { Name = "South B" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [large, southA, southB],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = large.Id, X = 24, Y = 24, ColumnSpan = 3, RowSpan = 3 }
            ]
        };
        var editor = new AreaNavigationEditorTabViewModel(area);

        Assert.True(editor.PlaceRoom(southA, 24, 444, out var firstNotice, createAutoTraversals: true));
        Assert.Null(firstNotice);
        var first = Assert.Single(editor.TraversalConnections);
        var forward = first.RoomAId == large.Id ? first.BaseTraversalDirectionFromA
            : TraversalWizardApplyUtility.OppositeDirection(first.BaseTraversalDirectionFromA);
        Assert.Equal(Direction10.South, forward);

        Assert.True(editor.PlaceRoom(southB, 204, 444, out var secondNotice, createAutoTraversals: true));
        Assert.Contains("Manage Traversals", secondNotice);
        Assert.Single(editor.TraversalConnections.Where(connection =>
            connection.RoomAId == large.Id || connection.RoomBId == large.Id));
    }

    [Fact]
    public void OrdinaryWizardRemainsAvailableForOneCellNeighborsButNotMultiCellNeighbors()
    {
        var first = new Room { Name = "First" };
        var second = new Room { Name = "Second" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [first, second],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = first.Id, X = 24, Y = 24 },
                new AreaRoomPlacement { RoomId = second.Id, X = 204, Y = 24 }
            ]
        };
        var editor = new AreaNavigationEditorTabViewModel(area);
        Assert.True(editor.CanUseOrdinaryTraversalWizard(first.Id));

        var secondPlacement = editor.RoomPlacements.Single(p => p.Room.Id == second.Id);
        Assert.True(editor.TryResizePlacement(secondPlacement, 2, 1, out _));

        Assert.True(editor.HasImmediateAdjacentRoom(first.Id));
        Assert.False(editor.CanUseOrdinaryTraversalWizard(first.Id));
        Assert.False(editor.CanUseOrdinaryTraversalWizard(second.Id));
    }

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

