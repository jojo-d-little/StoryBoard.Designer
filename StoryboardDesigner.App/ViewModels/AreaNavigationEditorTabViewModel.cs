using System.Collections.ObjectModel;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public sealed class AreaNavigationEditorTabViewModel : ViewModelBase
{
    private const double CellWidth = AreaMapGeometry.CellWidth;
    private const double CellHeight = AreaMapGeometry.CellHeight;
    private const double GridOriginX = AreaMapGeometry.OriginX;
    private const double GridOriginY = AreaMapGeometry.OriginY;
    private readonly Stack<AreaNavigationSnapshot> _undoStack = new();
    private AreaNavigationSnapshot? _activePlacementDragSnapshot;
    private Guid? _activePlacementDragRoomId;
    private int _gridColumns = 8;
    private int _gridRows = 6;
    private double _zoom = 1.0;
    private Guid? _selectedTraversalSourceRoomId;
    private int _selectedFloorElevation;
    private readonly Dictionary<Guid, TraversalValidationIssueState> _traversalValidationByConnectionId = new();

    public AreaNavigationEditorTabViewModel(Area area)
    {
        Area = area;
        Rooms = new ObservableCollection<Room>(area.Rooms);
        TraversalConnections = new ObservableCollection<TraversalConnection>(area.TraversalConnections);
        RoomPlacements = new ObservableCollection<AreaRoomPlacementViewModel>(
            area.RoomPlacements
                .Where(placement => area.Rooms.Any(room => room.Id == placement.RoomId))
                .Select(placement => new AreaRoomPlacementViewModel(ResolveRoom(placement.RoomId), placement)));
        EnsureGridContainsPlacements();
        VisibleRoomPlacements = new ObservableCollection<AreaRoomPlacementViewModel>();
        _selectedFloorElevation = 0;
        RefreshVisibleRoomPlacements();
        NavigationArrows = new ObservableCollection<NavigationArrowViewModel>();
        RebuildNavigationArrows();
    }

    public Area Area { get; }

    public ObservableCollection<Room> Rooms { get; }

    public ObservableCollection<TraversalConnection> TraversalConnections { get; }

    public ObservableCollection<AreaRoomPlacementViewModel> RoomPlacements { get; }

    public ObservableCollection<AreaRoomPlacementViewModel> VisibleRoomPlacements { get; }

    public ObservableCollection<NavigationArrowViewModel> NavigationArrows { get; }

    public string Title => Area.Name;

    public Guid? SelectedTraversalSourceRoomId
    {
        get => _selectedTraversalSourceRoomId;
        private set
        {
            if (_selectedTraversalSourceRoomId == value)
            {
                return;
            }

            _selectedTraversalSourceRoomId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTraversalSourceText));
        }
    }

    public string SelectedTraversalSourceText
    {
        get
        {
            if (!SelectedTraversalSourceRoomId.HasValue)
            {
                return "Traversal source: Auto nearest";
            }

            var roomName = Area.Rooms
                .FirstOrDefault(room => room.Id == SelectedTraversalSourceRoomId.Value)
                ?.Name;
            return string.IsNullOrWhiteSpace(roomName)
                ? "Traversal source: Auto nearest"
                : $"Traversal source: {roomName}";
        }
    }

    public int SelectedFloorElevation
    {
        get => _selectedFloorElevation;
        private set
        {
            if (_selectedFloorElevation == value)
            {
                return;
            }

            _selectedFloorElevation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedFloorText));
        }
    }

    public string SelectedFloorText => $"Floor: {SelectedFloorElevation}";

    public int GridColumns
    {
        get => _gridColumns;
        private set
        {
            if (_gridColumns == value)
            {
                return;
            }

            _gridColumns = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanvasWidth));
        }
    }

    public int GridRows
    {
        get => _gridRows;
        private set
        {
            if (_gridRows == value)
            {
                return;
            }

            _gridRows = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanvasHeight));
        }
    }

    public double CanvasWidth => (GridOriginX * 2) + (GridColumns * CellWidth);

    public double CanvasHeight => (GridOriginY * 2) + (GridRows * CellHeight);

    public double Zoom
    {
        get => _zoom;
        private set
        {
            var clamped = Math.Clamp(value, 0.25, 4.0);
            if (Math.Abs(_zoom - clamped) < 0.001)
            {
                return;
            }

            _zoom = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ZoomPercentText));
        }
    }

    public string ZoomPercentText => $"{Math.Round(Zoom * 100):0}%";

    public void ZoomIn()
    {
        Zoom *= 1.2;
    }

    public void ZoomOut()
    {
        Zoom /= 1.2;
    }

    public void ResetZoom()
    {
        Zoom = 1.0;
    }

    public void FitToViewport(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var widthScale = viewportWidth / CanvasWidth;
        var heightScale = viewportHeight / CanvasHeight;
        Zoom = Math.Min(widthScale, heightScale);
    }

    public void GrowGrid()
    {
        GridColumns += 2;
        GridRows += 2;
    }

    public bool TryUndoLastAction()
    {
        if (_undoStack.Count == 0)
        {
            return false;
        }

        var snapshot = _undoStack.Pop();
        RestoreSnapshot(snapshot);
        RebuildNavigationArrows();
        return true;
    }

    public void AddTraversalConnection()
    {
        ExecuteWithUndo(() =>
        {
            var created = TryCreateTraversalFromProximity(SelectedTraversalSourceRoomId);
            if (created is null)
            {
                return;
            }

            Area.TraversalConnections.Add(created);
            TraversalConnections.Add(created);
        });
        RebuildNavigationArrows();
    }

    public void SetTraversalSourceRoom(Guid roomId)
    {
        if (Area.Rooms.All(room => room.Id != roomId))
        {
            SelectedTraversalSourceRoomId = null;
            RefreshTraversalSourceSelection();
            return;
        }

        SelectedTraversalSourceRoomId = roomId;
        RefreshTraversalSourceSelection();
    }

    public void IncreaseSelectedFloorElevation()
    {
        SetSelectedFloorElevation(SelectedFloorElevation + 1);
    }

    public void DecreaseSelectedFloorElevation()
    {
        SetSelectedFloorElevation(SelectedFloorElevation - 1);
    }

    public void SetSelectedFloorElevation(int floorElevation)
    {
        SelectedFloorElevation = floorElevation;
        RefreshVisibleRoomPlacements();
        RefreshTraversalSourceSelection();
        RebuildNavigationArrows();
    }

    public void RemoveTraversalConnection(TraversalConnection connection)
    {
        ExecuteWithUndo(() =>
        {
            Area.TraversalConnections.Remove(connection);
            TraversalConnections.Remove(connection);
        });
        RebuildNavigationArrows();
    }

    public bool PlaceRoom(Room room, double x, double y, out string? validationMessage, bool createAutoTraversals = true)
    {
        validationMessage = null;
        string? autoTraversalNotice = null;
        var (targetCol, targetRow) = SnapToGridCell(x, y);
        var existing = RoomPlacements.FirstOrDefault(p => p.Room.Id == room.Id);
        var footprint = new AreaMapFootprint(targetCol, targetRow,
            existing?.ColumnSpan ?? 1, existing?.RowSpan ?? 1, SelectedFloorElevation);
        if (!CanPlace(footprint, room.Id, out validationMessage))
        {
            return false;
        }

        if (existing is not null)
        {
            ExecuteWithUndo(() =>
            {
                existing.FloorElevation = SelectedFloorElevation;
                existing.X = ToCanvasX(targetCol);
                existing.Y = ToCanvasY(targetRow);
                if (createAutoTraversals)
                {
                    autoTraversalNotice = EnsureAutoTraversalsForRoom(existing.Room.Id);
                }
            });
            RefreshVisibleRoomPlacements();
            RebuildNavigationArrows();
            validationMessage = autoTraversalNotice;
            return true;
        }

        ExecuteWithUndo(() =>
        {
            var placement = new AreaRoomPlacement
            {
                RoomId = room.Id,
                X = ToCanvasX(targetCol),
                Y = ToCanvasY(targetRow),
                FloorElevation = SelectedFloorElevation,
                ColumnSpan = 1,
                RowSpan = 1
            };

            Area.RoomPlacements.Add(placement);
            var created = new AreaRoomPlacementViewModel(room, placement);
            RoomPlacements.Add(created);
            if (createAutoTraversals)
            {
                autoTraversalNotice = EnsureAutoTraversalsForRoom(created.Room.Id);
            }
        });
        RefreshVisibleRoomPlacements();
        RebuildNavigationArrows();
        validationMessage = autoTraversalNotice;
        return true;
    }

    public bool HasImmediateAdjacentRoom(Guid roomId)
    {
        var source = RoomPlacements.FirstOrDefault(p => p.Room.Id == roomId);
        if (source is null || source.FloorElevation != SelectedFloorElevation)
        {
            return false;
        }

        foreach (var target in VisibleRoomPlacements)
        {
            if (target.Room.Id == roomId)
            {
                continue;
            }

            if (source.Footprint.TryGetAdjacentOffset(target.Footprint,
                Area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out _, out _)) return true;
        }

        return false;
    }

    public bool CanUseOrdinaryTraversalWizard(Guid roomId)
    {
        var source = VisibleRoomPlacements.FirstOrDefault(placement => placement.Room.Id == roomId);
        if (source is null || source.ColumnSpan != 1 || source.RowSpan != 1) return false;

        return !VisibleRoomPlacements.Any(target => target.Room.Id != roomId
            && (target.ColumnSpan != 1 || target.RowSpan != 1)
            && source.Footprint.TryGetAdjacentOffset(target.Footprint,
                Area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out _, out _));
    }

    public bool UpdatePlacement(AreaRoomPlacementViewModel placement, double x, double y)
    {
        return TryPreviewPlacement(placement, x, y);
    }

    public void BeginPlacementDrag(AreaRoomPlacementViewModel placement)
    {
        _activePlacementDragSnapshot = TakeSnapshot();
        _activePlacementDragRoomId = placement.Room.Id;
    }

    public bool TryPreviewPlacement(AreaRoomPlacementViewModel placement, double x, double y)
    {
        var (targetCol, targetRow) = SnapToGridCell(x, y);
        var footprint = new AreaMapFootprint(targetCol, targetRow,
            placement.ColumnSpan, placement.RowSpan, placement.FloorElevation);
        if (!CanPlace(footprint, placement.Room.Id, out _))
        {
            return false;
        }

        placement.X = ToCanvasX(targetCol);
        placement.Y = ToCanvasY(targetRow);
        RebuildNavigationArrows();
        return true;
    }

    public bool TryResizePlacement(AreaRoomPlacementViewModel placement, int columnSpan, int rowSpan, out string? validationMessage)
    {
        validationMessage = null;
        if (!AreaMapGeometry.IsValidSpan(columnSpan) || !AreaMapGeometry.IsValidSpan(rowSpan))
        {
            validationMessage = "Room spans must be between 1 and 3 cells.";
            return false;
        }

        var current = placement.Footprint;
        var resized = current with { ColumnSpan = columnSpan, RowSpan = rowSpan };
        if (!CanPlace(resized, placement.Room.Id, out validationMessage)) return false;
        if (placement.ColumnSpan == columnSpan && placement.RowSpan == rowSpan) return true;

        ExecuteWithUndo(() =>
        {
            placement.ColumnSpan = columnSpan;
            placement.RowSpan = rowSpan;
        });
        RebuildNavigationArrows();
        return true;
    }

    public void CancelPlacementDrag(AreaRoomPlacementViewModel placement)
    {
        if (_activePlacementDragSnapshot is null || _activePlacementDragRoomId != placement.Room.Id)
        {
            return;
        }

        RestoreSnapshot(_activePlacementDragSnapshot);
        _activePlacementDragSnapshot = null;
        _activePlacementDragRoomId = null;
        RebuildNavigationArrows();
    }

    public bool CommitPlacementDrag(AreaRoomPlacementViewModel placement, bool deleteConnectedTraversals, out int deletedTraversalCount)
    {
        deletedTraversalCount = 0;

        if (_activePlacementDragSnapshot is null || _activePlacementDragRoomId != placement.Room.Id)
        {
            return false;
        }

        var originalPlacement = _activePlacementDragSnapshot.Placements
            .FirstOrDefault(saved => saved.RoomId == placement.Room.Id);

        if (originalPlacement is null)
        {
            _activePlacementDragSnapshot = null;
            _activePlacementDragRoomId = null;
            return false;
        }

        var moved = Math.Abs(originalPlacement.X - placement.X) >= 0.01
            || Math.Abs(originalPlacement.Y - placement.Y) >= 0.01;

        if (!moved)
        {
            _activePlacementDragSnapshot = null;
            _activePlacementDragRoomId = null;
            return false;
        }

        _undoStack.Push(_activePlacementDragSnapshot);

        if (deleteConnectedTraversals)
        {
            var connectionsToRemove = TraversalConnections
                .Where(connection => connection.RoomAId == placement.Room.Id || connection.RoomBId == placement.Room.Id)
                .ToList();

            deletedTraversalCount = connectionsToRemove.Count;
            foreach (var connection in connectionsToRemove)
            {
                Area.TraversalConnections.Remove(connection);
                TraversalConnections.Remove(connection);
            }
        }

        _activePlacementDragSnapshot = null;
        _activePlacementDragRoomId = null;
        RebuildNavigationArrows();
        return true;
    }

    public int GetConnectedTraversalCount(Guid roomId)
    {
        return TraversalConnections.Count(connection => connection.RoomAId == roomId || connection.RoomBId == roomId);
    }

    public void RemoveRoomFromMap(Room room)
    {
        var placement = RoomPlacements.FirstOrDefault(p => p.Room.Id == room.Id);
        if (placement is null)
        {
            return;
        }

        ExecuteWithUndo(() =>
        {
            Area.RoomPlacements.RemoveAll(p => p.RoomId == room.Id);
            RoomPlacements.Remove(placement);

            if (SelectedTraversalSourceRoomId == room.Id)
            {
                SelectedTraversalSourceRoomId = null;
            }

            var connectionsToRemove = TraversalConnections
                .Where(connection => connection.RoomAId == room.Id || connection.RoomBId == room.Id)
                .ToList();
            foreach (var connection in connectionsToRemove)
            {
                Area.TraversalConnections.Remove(connection);
                TraversalConnections.Remove(connection);
            }
        });

        RefreshVisibleRoomPlacements();
        RebuildNavigationArrows();
    }

    public void ChangeTraversalDestination(TraversalConnection connection, Guid sourceRoomId, Guid destinationRoomId)
    {
        if (sourceRoomId == destinationRoomId)
        {
            return;
        }

        ExecuteWithUndo(() =>
        {
            if (connection.RoomAId == sourceRoomId)
            {
                connection.RoomBId = destinationRoomId;
            }
            else if (connection.RoomBId == sourceRoomId)
            {
                connection.RoomAId = destinationRoomId;
            }

            UpdateTraversalDirectionFromPlacement(connection);
        });
        RebuildNavigationArrows();
    }

    public void RefreshNavigationArrows()
    {
        RebuildNavigationArrows();
    }

    public void SetTraversalValidationIssues(IReadOnlyList<ProjectValidationIssue> issues)
    {
        _traversalValidationByConnectionId.Clear();

        foreach (var issue in issues)
        {
            if (!TryExtractTraversalConnectionId(issue.Path, out var connectionId))
            {
                continue;
            }

            if (!_traversalValidationByConnectionId.TryGetValue(connectionId, out var state))
            {
                state = new TraversalValidationIssueState();
                _traversalValidationByConnectionId[connectionId] = state;
            }

            if (issue.Severity == ValidationSeverity.Error)
            {
                state.ErrorCount++;
            }
            else
            {
                state.WarningCount++;
            }

            var preview = BuildValidationPreviewLine(issue);
            if (!state.Messages.Contains(preview, StringComparer.OrdinalIgnoreCase))
            {
                state.Messages.Add(preview);
            }
        }

        ApplyTraversalValidationProjectionToArrows();
    }

    public void ClearTraversalValidationIssues()
    {
        if (_traversalValidationByConnectionId.Count == 0)
        {
            return;
        }

        _traversalValidationByConnectionId.Clear();
        ApplyTraversalValidationProjectionToArrows();
    }

    private Room ResolveRoom(Guid roomId)
    {
        return Area.Rooms.First(room => room.Id == roomId);
    }

    private bool CanPlace(AreaMapFootprint footprint, Guid roomId, out string? validationMessage)
    {
        if (!footprint.Fits(GridColumns, GridRows))
        {
            validationMessage = "The room footprint extends beyond the grid. Grow the grid or choose another cell.";
            return false;
        }

        if (RoomPlacements.Any(other => other.Room.Id != roomId && footprint.Overlaps(other.Footprint)))
        {
            validationMessage = "The room footprint overlaps another room on this floor.";
            return false;
        }

        validationMessage = null;
        return true;
    }

    private void EnsureGridContainsPlacements()
    {
        if (RoomPlacements.Count == 0) return;
        GridColumns = Math.Max(GridColumns, RoomPlacements.Max(placement => placement.Footprint.Right));
        GridRows = Math.Max(GridRows, RoomPlacements.Max(placement => placement.Footprint.Bottom));
    }

    private string? EnsureAutoTraversalsForRoom(Guid roomId)
    {
        var source = RoomPlacements.FirstOrDefault(p => p.Room.Id == roomId);
        if (source is null)
        {
            return null;
        }

        var floorPlacements = RoomPlacements.Where(placement => placement.FloorElevation == source.FloorElevation).ToList();
        var skippedAmbiguous = false;
        foreach (var target in floorPlacements)
        {
            if (target.Room.Id == roomId)
            {
                continue;
            }

            if (!source.Footprint.TryGetAdjacentOffset(target.Footprint,
                    Area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out var dx, out var dy))
            {
                continue;
            }

            var forwardDirection = ToDirection(dx, dy);
            var reverseDirection = InvertDirection(forwardDirection);
            var (roomAId, roomBId) = ToOrderedPair(source.Room.Id, target.Room.Id);
            var existingPair = TraversalConnections.FirstOrDefault(connection =>
                connection.RoomAId == roomAId && connection.RoomBId == roomBId);
            if (existingPair is not null)
            {
                UpdateTraversalDirectionFromPlacement(existingPair);
                continue;
            }
            if (HasAmbiguousAdjacentSide(source, forwardDirection, floorPlacements)
                || HasAmbiguousAdjacentSide(target, reverseDirection, floorPlacements)
                || HasOutgoingDirectionConflict(source.Room.Id, target.Room.Id, ToDirection10(forwardDirection))
                || HasOutgoingDirectionConflict(target.Room.Id, source.Room.Id, ToDirection10(reverseDirection)))
            {
                skippedAmbiguous = true;
                continue;
            }
            EnsureTraversalExists(source.Room.Id, target.Room.Id, forwardDirection);
        }

        return skippedAmbiguous
            ? "Some adjacent rooms share an exit side. Use Manage Traversals to select each room pair and its doors."
            : null;
    }

    private bool HasAmbiguousAdjacentSide(AreaRoomPlacementViewModel source, Direction direction,
        IReadOnlyCollection<AreaRoomPlacementViewModel> floorPlacements)
    {
        return floorPlacements.Count(target => target.Room.Id != source.Room.Id
            && source.Footprint.TryGetAdjacentOffset(target.Footprint,
                Area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out var dx, out var dy)
            && ToDirection(dx, dy) == direction) > 1;
    }

    private bool HasOutgoingDirectionConflict(Guid sourceRoomId, Guid destinationRoomId, Direction10 direction)
    {
        return TraversalConnections.Any(connection =>
            (connection.RoomAId == sourceRoomId && connection.RoomBId != destinationRoomId
                && connection.TraversalAccessMode is (TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayAtoB)
                && connection.BaseTraversalDirectionFromA == direction)
            || (connection.RoomBId == sourceRoomId && connection.RoomAId != destinationRoomId
                && connection.TraversalAccessMode is (TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayBtoA)
                && InvertDirection(connection.BaseTraversalDirectionFromA) == direction));
    }

    private void EnsureTraversalExists(Guid fromRoomId, Guid toRoomId, Direction directionFromSourceToTarget)
    {
        if (fromRoomId == toRoomId)
        {
            return;
        }

        var (roomAId, roomBId) = ToOrderedPair(fromRoomId, toRoomId);
        var existing = TraversalConnections
            .FirstOrDefault(connection => connection.RoomAId == roomAId && connection.RoomBId == roomBId);
        if (existing is not null)
        {
            UpdateTraversalDirectionFromPlacement(existing);
            return;
        }

        var baseDirectionFromA = roomAId == fromRoomId
            ? directionFromSourceToTarget
            : InvertDirection(directionFromSourceToTarget);

        var connection = new TraversalConnection
        {
            RoomAId = roomAId,
            RoomBId = roomBId,
            BaseTraversalDirectionFromA = ToDirection10(baseDirectionFromA),
            TraversalAccessMode = TraversalAccessMode.TwoWay
        };

        Area.TraversalConnections.Add(connection);
        TraversalConnections.Add(connection);
    }

    private void RebuildNavigationArrows()
    {
        NavigationArrows.Clear();

        foreach (var connection in TraversalConnections)
        {
            AddArrowForLeg(connection, connection.RoomAId, connection.RoomBId, connection.BaseTraversalDirectionFromA, connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayAtoB);
            AddArrowForLeg(connection, connection.RoomBId, connection.RoomAId, InvertDirection(connection.BaseTraversalDirectionFromA), connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayBtoA);
        }

        RefreshVerticalIndicators();
        ApplyTraversalValidationProjectionToArrows();
    }

    private void RefreshVerticalIndicators()
    {
        var placementsByRoomId = RoomPlacements
            .GroupBy(placement => placement.Room.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var placement in RoomPlacements)
        {
            placement.HasRoomAbove = RoomPlacements.Any(other =>
                other.Room.Id != placement.Room.Id
                && other.FloorElevation > placement.FloorElevation
                && placement.Footprint.Intersects(other.Footprint));

            placement.HasRoomBelow = RoomPlacements.Any(other =>
                other.Room.Id != placement.Room.Id
                && other.FloorElevation < placement.FloorElevation
                && placement.Footprint.Intersects(other.Footprint));

            var hasTraversalUp = false;
            var hasTraversalDown = false;

            foreach (var connection in TraversalConnections)
            {
                var isRoomA = connection.RoomAId == placement.Room.Id;
                var isRoomB = connection.RoomBId == placement.Room.Id;
                if (!isRoomA && !isRoomB)
                {
                    continue;
                }

                var legEnabled = isRoomA
                    ? connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayAtoB
                    : connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayBtoA;
                if (!legEnabled)
                {
                    continue;
                }

                var targetRoomId = isRoomA ? connection.RoomBId : connection.RoomAId;
                if (!placementsByRoomId.TryGetValue(targetRoomId, out var targetPlacement))
                {
                    continue;
                }

                if (targetPlacement.FloorElevation > placement.FloorElevation)
                {
                    hasTraversalUp = true;
                }
                else if (targetPlacement.FloorElevation < placement.FloorElevation)
                {
                    hasTraversalDown = true;
                }
            }

            placement.HasTraversalUp = hasTraversalUp;
            placement.HasTraversalDown = hasTraversalDown;
        }
    }

    private void AddArrowForLeg(
        TraversalConnection connection,
        Guid sourceRoomId,
        Guid destinationRoomId,
        Direction10 direction,
        bool isEnabled)
    {
        if (!isEnabled)
        {
            return;
        }

        if (!TryToDirection(direction, out var planarDirection))
        {
            return;
        }

        var fromPlacement = RoomPlacements.FirstOrDefault(p => p.Room.Id == sourceRoomId);
        var toPlacement = RoomPlacements.FirstOrDefault(p => p.Room.Id == destinationRoomId);
        if (fromPlacement is null || toPlacement is null)
        {
            return;
        }

        if (fromPlacement.FloorElevation != SelectedFloorElevation
            || toPlacement.FloorElevation != SelectedFloorElevation)
        {
            return;
        }

        var fromHalfWidth = fromPlacement.CardWidth / 2;
        var fromHalfHeight = fromPlacement.CardHeight / 2;
        var toHalfWidth = toPlacement.CardWidth / 2;
        var toHalfHeight = toPlacement.CardHeight / 2;
        var fromCenterX = fromPlacement.X + fromHalfWidth;
        var fromCenterY = fromPlacement.Y + fromHalfHeight;
        var toCenterX = toPlacement.X + toHalfWidth;
        var toCenterY = toPlacement.Y + toHalfHeight;
        var dx = toCenterX - fromCenterX;
        var dy = toCenterY - fromCenterY;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1)
        {
            return;
        }

        double midX;
        double midY;
        double arrowDx;
        double arrowDy;
        if (fromPlacement.Footprint.TryGetAdjacentOffset(toPlacement.Footprint,
                Area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out var adjacentDx, out var adjacentDy)
            && (adjacentDx == 0 || adjacentDy == 0)
            && ToDirection(adjacentDx, adjacentDy) == planarDirection
            && TryGetSharedSideMidpoint(fromPlacement, toPlacement, adjacentDx, adjacentDy, out midX, out midY))
        {
            arrowDx = adjacentDx;
            arrowDy = adjacentDy;
        }
        else
        {
            // Keep the center-to-center projection for explicit nonadjacent links.
            var fromScale = Math.Min(dx == 0 ? double.PositiveInfinity : fromHalfWidth / Math.Abs(dx),
                dy == 0 ? double.PositiveInfinity : fromHalfHeight / Math.Abs(dy));
            var toScale = Math.Min(dx == 0 ? double.PositiveInfinity : toHalfWidth / Math.Abs(dx),
                dy == 0 ? double.PositiveInfinity : toHalfHeight / Math.Abs(dy));
            midX = (fromCenterX + dx * fromScale + toCenterX - dx * toScale) / 2;
            midY = (fromCenterY + dy * fromScale + toCenterY - dy * toScale) / 2;
            arrowDx = dx / length;
            arrowDy = dy / length;
        }

        // Opposite legs use opposite normals, keeping their icons visible side by side.
        var offsetX = -arrowDy * 10;
        var offsetY = arrowDx * 10;

        var arrow = new NavigationArrowViewModel(connection)
        {
            SourceRoomId = sourceRoomId,
            DestinationRoomId = destinationRoomId,
            Direction = planarDirection,
            X = midX + offsetX,
            Y = midY + offsetY,
            Angle = Math.Atan2(arrowDy, arrowDx) * (180 / Math.PI)
        };

        ApplyTraversalValidationProjection(arrow);
        NavigationArrows.Add(arrow);
    }

    private static bool TryGetSharedSideMidpoint(
        AreaRoomPlacementViewModel from,
        AreaRoomPlacementViewModel to,
        int adjacentDx,
        int adjacentDy,
        out double x,
        out double y)
    {
        if (adjacentDy != 0)
        {
            var overlapLeft = Math.Max(from.X, to.X);
            var overlapRight = Math.Min(from.X + from.CardWidth, to.X + to.CardWidth);
            x = (overlapLeft + overlapRight) / 2;
            y = adjacentDy > 0
                ? (from.Y + from.CardHeight + to.Y) / 2
                : (to.Y + to.CardHeight + from.Y) / 2;
            return overlapRight > overlapLeft;
        }

        var overlapTop = Math.Max(from.Y, to.Y);
        var overlapBottom = Math.Min(from.Y + from.CardHeight, to.Y + to.CardHeight);
        x = adjacentDx > 0
            ? (from.X + from.CardWidth + to.X) / 2
            : (to.X + to.CardWidth + from.X) / 2;
        y = (overlapTop + overlapBottom) / 2;
        return overlapBottom > overlapTop;
    }

    private static string BuildValidationPreviewLine(ProjectValidationIssue issue)
    {
        var severity = issue.Severity == ValidationSeverity.Warning ? "Warning" : "Error";
        var ruleId = string.IsNullOrWhiteSpace(issue.RuleId) ? "(no-rule)" : issue.RuleId;
        var description = string.IsNullOrWhiteSpace(issue.Description) ? "Validation issue" : issue.Description.Trim();
        return $"[{severity} {ruleId}] {description}";
    }

    private static bool TryExtractTraversalConnectionId(string? path, out Guid connectionId)
    {
        connectionId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        const string marker = "/ TraversalConnection/";
        var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var segmentStart = markerIndex + marker.Length;
        if (segmentStart >= path.Length)
        {
            return false;
        }

        var remaining = path[segmentStart..].Trim();
        var slashIndex = remaining.IndexOf('/');
        var idSegment = slashIndex >= 0 ? remaining[..slashIndex].Trim() : remaining;
        return Guid.TryParse(idSegment, out connectionId);
    }

    private void ApplyTraversalValidationProjectionToArrows()
    {
        foreach (var arrow in NavigationArrows)
        {
            ApplyTraversalValidationProjection(arrow);
        }
    }

    private void ApplyTraversalValidationProjection(NavigationArrowViewModel arrow)
    {
        if (_traversalValidationByConnectionId.TryGetValue(arrow.Connection.TraversalConnectionId, out var state))
        {
            arrow.HasValidationError = state.ErrorCount > 0;
            arrow.HasValidationWarning = state.WarningCount > 0 && state.ErrorCount == 0;
            arrow.ValidationToolTip = BuildTraversalValidationToolTip(arrow.Connection.TraversalConnectionId, state);
            return;
        }

        arrow.HasValidationError = false;
        arrow.HasValidationWarning = false;
        arrow.ValidationToolTip = $"Traversal {arrow.Connection.TraversalConnectionId:N}";
    }

    private static string BuildTraversalValidationToolTip(Guid connectionId, TraversalValidationIssueState state)
    {
        var summary = $"Traversal {connectionId:N}: {state.ErrorCount} error(s), {state.WarningCount} warning(s).";
        if (state.Messages.Count == 0)
        {
            return summary;
        }

        var previewLines = state.Messages.Take(2).Select(static message => $"- {message}");
        var remaining = state.Messages.Count - 2;
        var preview = string.Join("\n", previewLines);
        if (remaining <= 0)
        {
            return $"{summary}\n{preview}";
        }

        return $"{summary}\n{preview}\n...and {remaining} more issue(s).";
    }

    private TraversalConnection? TryCreateTraversalFromProximity(Guid? preferredSourceRoomId)
    {
        var floorPlacements = VisibleRoomPlacements.ToList();

        if (floorPlacements.Count >= 2)
        {
            var sourcePlacements = preferredSourceRoomId.HasValue
                ? floorPlacements.Where(placement => placement.Room.Id == preferredSourceRoomId.Value).ToList()
                : new List<AreaRoomPlacementViewModel>();
            if (sourcePlacements.Count == 0)
            {
                sourcePlacements = floorPlacements;
            }

            var candidates = sourcePlacements
                .SelectMany(source => floorPlacements
                    .Where(target => target.Room.Id != source.Room.Id)
                    .Select(target => new
                    {
                        Source = source,
                        Target = target,
                        IsAdjacent = source.Footprint.TryGetAdjacentOffset(target.Footprint,
                            Area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out var dx, out var dy),
                        Dx = dx,
                        Dy = dy
                    }))
                .Where(candidate => candidate.IsAdjacent)
                .Select(candidate => new
                {
                    candidate.Source,
                    candidate.Target,
                    Distance = Math.Abs(candidate.Dx) + Math.Abs(candidate.Dy),
                    Direction = ToDirection(candidate.Dx, candidate.Dy)
                })
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Source.Room.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Target.Room.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var candidate in candidates)
            {
                var (roomAId, roomBId) = ToOrderedPair(candidate.Source.Room.Id, candidate.Target.Room.Id);
                var exists = TraversalConnections.Any(connection => connection.RoomAId == roomAId && connection.RoomBId == roomBId);
                if (exists)
                {
                    continue;
                }

                return new TraversalConnection
                {
                    RoomAId = roomAId,
                    RoomBId = roomBId,
                    BaseTraversalDirectionFromA = roomAId == candidate.Source.Room.Id
                        ? ToDirection10(candidate.Direction)
                        : ToDirection10(InvertDirection(candidate.Direction)),
                    TraversalAccessMode = TraversalAccessMode.TwoWay
                };
            }
        }

        var floorRooms = floorPlacements.Select(placement => placement.Room).ToList();
        var firstRoom = preferredSourceRoomId.HasValue
            ? floorRooms.FirstOrDefault(room => room.Id == preferredSourceRoomId.Value)
            : null;
        firstRoom ??= floorRooms.FirstOrDefault();
        var secondRoom = floorRooms.Skip(1).FirstOrDefault();

        if (firstRoom is null || secondRoom is null)
        {
            firstRoom = preferredSourceRoomId.HasValue
                ? Area.Rooms.FirstOrDefault(room => room.Id == preferredSourceRoomId.Value)
                : null;
            firstRoom ??= Area.Rooms.FirstOrDefault();
            secondRoom = Area.Rooms.Skip(1).FirstOrDefault();
        }

        if (firstRoom is not null && secondRoom is not null && secondRoom.Id == firstRoom.Id)
        {
            secondRoom = Area.Rooms.FirstOrDefault(room => room.Id != firstRoom.Id);
        }
        if (firstRoom is null || secondRoom is null)
        {
            return null;
        }

        var (orderedA, orderedB) = ToOrderedPair(firstRoom.Id, secondRoom.Id);
        return new TraversalConnection
        {
            RoomAId = orderedA,
            RoomBId = orderedB,
            BaseTraversalDirectionFromA = Direction10.North,
            TraversalAccessMode = TraversalAccessMode.TwoWay
        };
    }

    private void UpdateTraversalDirectionFromPlacement(TraversalConnection connection)
    {
        var placementA = RoomPlacements.FirstOrDefault(placement => placement.Room.Id == connection.RoomAId);
        var placementB = RoomPlacements.FirstOrDefault(placement => placement.Room.Id == connection.RoomBId);
        if (placementA is null || placementB is null)
        {
            return;
        }

        if (placementA.FloorElevation != placementB.FloorElevation)
        {
            return;
        }

        if (!placementA.Footprint.TryGetAdjacentOffset(placementB.Footprint,
                Area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out var dx, out var dy))
        {
            return;
        }

        connection.BaseTraversalDirectionFromA = ToDirection10(ToDirection(dx, dy));
    }

    private static (Guid roomAId, Guid roomBId) ToOrderedPair(Guid firstRoomId, Guid secondRoomId)
    {
        return string.CompareOrdinal(firstRoomId.ToString("N"), secondRoomId.ToString("N")) <= 0
            ? (firstRoomId, secondRoomId)
            : (secondRoomId, firstRoomId);
    }

    private static Direction InvertDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.NorthEast => Direction.SouthWest,
            Direction.East => Direction.West,
            Direction.SouthEast => Direction.NorthWest,
            Direction.South => Direction.North,
            Direction.SouthWest => Direction.NorthEast,
            Direction.West => Direction.East,
            Direction.NorthWest => Direction.SouthEast,
            _ => Direction.North
        };
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

    private (int col, int row) SnapToGridCell(double x, double y)
    {
        return AreaMapGeometry.ToGridCell(x, y);
    }

    private static double ToCanvasX(int col)
    {
        return GridOriginX + (col * CellWidth);
    }

    private static double ToCanvasY(int row)
    {
        return GridOriginY + (row * CellHeight);
    }

    private static Direction ToDirection(int dx, int dy)
    {
        return (dx, dy) switch
        {
            (0, -1) => Direction.North,
            (1, -1) => Direction.NorthEast,
            (1, 0) => Direction.East,
            (1, 1) => Direction.SouthEast,
            (0, 1) => Direction.South,
            (-1, 1) => Direction.SouthWest,
            (-1, 0) => Direction.West,
            (-1, -1) => Direction.NorthWest,
            _ => Direction.North
        };
    }

    private static Direction10 ToDirection10(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction10.North,
            Direction.NorthEast => Direction10.NorthEast,
            Direction.East => Direction10.East,
            Direction.SouthEast => Direction10.SouthEast,
            Direction.South => Direction10.South,
            Direction.SouthWest => Direction10.SouthWest,
            Direction.West => Direction10.West,
            Direction.NorthWest => Direction10.NorthWest,
            _ => Direction10.North
        };
    }

    private static bool TryToDirection(Direction10 direction, out Direction mappedDirection)
    {
        mappedDirection = direction switch
        {
            Direction10.North => Direction.North,
            Direction10.NorthEast => Direction.NorthEast,
            Direction10.East => Direction.East,
            Direction10.SouthEast => Direction.SouthEast,
            Direction10.South => Direction.South,
            Direction10.SouthWest => Direction.SouthWest,
            Direction10.West => Direction.West,
            Direction10.NorthWest => Direction.NorthWest,
            _ => Direction.North
        };

        return direction is not Direction10.Up and not Direction10.Down;
    }

    private void ExecuteWithUndo(Action action)
    {
        _undoStack.Push(TakeSnapshot());
        action();
    }

    private AreaNavigationSnapshot TakeSnapshot()
    {
        return new AreaNavigationSnapshot
        {
            TraversalConnections = TraversalConnections.Select(connection => new TraversalConnection
            {
                TraversalConnectionId = connection.TraversalConnectionId,
                RoomAId = connection.RoomAId,
                RoomBId = connection.RoomBId,
                BaseTraversalDirectionFromA = connection.BaseTraversalDirectionFromA,
                TraversalModeOverride = connection.TraversalModeOverride,
                TraversalAccessMode = connection.TraversalAccessMode,
                OpenStateBindingMode = connection.OpenStateBindingMode,
                TraversalStateFromA = new TraversalLegState
                {
                    OpenableObjectId = connection.TraversalStateFromA.OpenableObjectId,
                    OpenStatePolicy = connection.TraversalStateFromA.OpenStatePolicy,
                    SharedVariableId = connection.TraversalStateFromA.SharedVariableId,
                    AvailableActions = connection.TraversalStateFromA.AvailableActions.Select(CloneCommandAction).ToList(),
                    Variables = connection.TraversalStateFromA.Variables.Select(variable => new GamePropertyDefinition
                    {
                        Name = variable.Name,
                        DefaultValue = variable.DefaultValue,
                        ValueRestriction = variable.ValueRestriction,
                        Lifetime = variable.Lifetime,
                        SharedVariableId = variable.SharedVariableId,
                        Computation = GamePropertyComputationCloner.Clone(variable.Computation)
                    }).ToList()
                },
                TraversalStateFromB = new TraversalLegState
                {
                    OpenableObjectId = connection.TraversalStateFromB.OpenableObjectId,
                    OpenStatePolicy = connection.TraversalStateFromB.OpenStatePolicy,
                    SharedVariableId = connection.TraversalStateFromB.SharedVariableId,
                    AvailableActions = connection.TraversalStateFromB.AvailableActions.Select(CloneCommandAction).ToList(),
                    Variables = connection.TraversalStateFromB.Variables.Select(variable => new GamePropertyDefinition
                    {
                        Name = variable.Name,
                        DefaultValue = variable.DefaultValue,
                        ValueRestriction = variable.ValueRestriction,
                        Lifetime = variable.Lifetime,
                        SharedVariableId = variable.SharedVariableId,
                        Computation = GamePropertyComputationCloner.Clone(variable.Computation)
                    }).ToList()
                },
                Variables = connection.Variables.Select(variable => new GamePropertyDefinition
                {
                    Name = variable.Name,
                    DefaultValue = variable.DefaultValue,
                    ValueRestriction = variable.ValueRestriction,
                    Lifetime = variable.Lifetime,
                    Computation = GamePropertyComputationCloner.Clone(variable.Computation)
                }).ToList()
            }).ToList(),
            Placements = RoomPlacements.Select(placement => new AreaRoomPlacement
            {
                RoomId = placement.Room.Id,
                X = placement.X,
                Y = placement.Y,
                FloorElevation = placement.FloorElevation,
                ColumnSpan = placement.ColumnSpan,
                RowSpan = placement.RowSpan
            }).ToList()
        };
    }

    private void RestoreSnapshot(AreaNavigationSnapshot snapshot)
    {
        Area.TraversalConnections = snapshot.TraversalConnections.Select(connection => new TraversalConnection
        {
            TraversalConnectionId = connection.TraversalConnectionId,
            RoomAId = connection.RoomAId,
            RoomBId = connection.RoomBId,
            BaseTraversalDirectionFromA = connection.BaseTraversalDirectionFromA,
            TraversalModeOverride = connection.TraversalModeOverride,
            TraversalAccessMode = connection.TraversalAccessMode,
            OpenStateBindingMode = connection.OpenStateBindingMode,
            TraversalStateFromA = new TraversalLegState
            {
                OpenableObjectId = connection.TraversalStateFromA.OpenableObjectId,
                OpenStatePolicy = connection.TraversalStateFromA.OpenStatePolicy,
                SharedVariableId = connection.TraversalStateFromA.SharedVariableId,
                AvailableActions = connection.TraversalStateFromA.AvailableActions.Select(CloneCommandAction).ToList(),
                Variables = connection.TraversalStateFromA.Variables.Select(variable => new GamePropertyDefinition
                {
                    Name = variable.Name,
                    DefaultValue = variable.DefaultValue,
                    ValueRestriction = variable.ValueRestriction,
                    Lifetime = variable.Lifetime,
                    SharedVariableId = variable.SharedVariableId,
                    Computation = GamePropertyComputationCloner.Clone(variable.Computation)
                }).ToList()
            },
            TraversalStateFromB = new TraversalLegState
            {
                OpenableObjectId = connection.TraversalStateFromB.OpenableObjectId,
                OpenStatePolicy = connection.TraversalStateFromB.OpenStatePolicy,
                SharedVariableId = connection.TraversalStateFromB.SharedVariableId,
                AvailableActions = connection.TraversalStateFromB.AvailableActions.Select(CloneCommandAction).ToList(),
                Variables = connection.TraversalStateFromB.Variables.Select(variable => new GamePropertyDefinition
                {
                    Name = variable.Name,
                    DefaultValue = variable.DefaultValue,
                    ValueRestriction = variable.ValueRestriction,
                    Lifetime = variable.Lifetime,
                    SharedVariableId = variable.SharedVariableId,
                    Computation = GamePropertyComputationCloner.Clone(variable.Computation)
                }).ToList()
            },
            Variables = connection.Variables.Select(variable => new GamePropertyDefinition
            {
                Name = variable.Name,
                DefaultValue = variable.DefaultValue,
                ValueRestriction = variable.ValueRestriction,
                Lifetime = variable.Lifetime,
                Computation = GamePropertyComputationCloner.Clone(variable.Computation)
            }).ToList()
        }).ToList();

        TraversalConnections.Clear();
        foreach (var connection in Area.TraversalConnections)
        {
            TraversalConnections.Add(connection);
        }

        Area.RoomPlacements = snapshot.Placements.Select(placement => new AreaRoomPlacement
        {
            RoomId = placement.RoomId,
            X = placement.X,
            Y = placement.Y,
            FloorElevation = placement.FloorElevation,
            ColumnSpan = placement.ColumnSpan,
            RowSpan = placement.RowSpan
        }).ToList();

        RoomPlacements.Clear();
        foreach (var placement in Area.RoomPlacements)
        {
            if (Area.Rooms.FirstOrDefault(room => room.Id == placement.RoomId) is { } room)
            {
                RoomPlacements.Add(new AreaRoomPlacementViewModel(room, placement));
            }
        }

        EnsureGridContainsPlacements();

        RefreshVisibleRoomPlacements();

        if (!SelectedTraversalSourceRoomId.HasValue
            || Area.RoomPlacements.All(placement => placement.RoomId != SelectedTraversalSourceRoomId.Value))
        {
            SelectedTraversalSourceRoomId = null;
        }

        RefreshTraversalSourceSelection();
    }

    private void RefreshVisibleRoomPlacements()
    {
        VisibleRoomPlacements.Clear();
        foreach (var placement in RoomPlacements.Where(p => p.FloorElevation == SelectedFloorElevation))
        {
            VisibleRoomPlacements.Add(placement);
        }
    }

    private void RefreshTraversalSourceSelection()
    {
        foreach (var placement in RoomPlacements)
        {
            placement.IsTraversalSourceSelected = SelectedTraversalSourceRoomId.HasValue
                && placement.Room.Id == SelectedTraversalSourceRoomId.Value;
        }
    }

    private static CommandAction CloneCommandAction(CommandAction action)
    {
        var isSetFlagAction = action.ActionType == CommandActionType.SetFlag;
        var clone = new CommandAction
        {
            Id = action.Id,
            Name = action.Name,
            ActionType = action.ActionType,
            NoVerbLinkage = action.NoVerbLinkage,
            Verbs = action.Verbs?.ToList() ?? new List<string>(),
            Direction = action.Direction,
            FlagName = isSetFlagAction
                ? ActionPayloadAccessors.GetSetFlagName(action)
                : ActionPayloadAccessors.GetCheckPropertyName(action),
            FlagValue = isSetFlagAction
                ? ActionPayloadAccessors.GetSetFlagValue(action)
                : ActionPayloadAccessors.GetCheckExpectedValue(action),
            GamePropertyName = ActionPayloadAccessors.GetSetPropertyName(action),
            GamePropertyValue = ActionPayloadAccessors.GetSetPropertyValue(action),
            TargetContainerId = action.TargetContainerId,
            SynonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(action),
            CompositeTargetObjectId = action.CompositeTargetObjectId,
            CompositeRecipeId = action.CompositeRecipeId,
            CompositeRequiredPartObjectIds = action.CompositeRequiredPartObjectIds?.ToList() ?? new List<Guid>(),
            CompositeStrictPartCountEnforcement = action.CompositeStrictPartCountEnforcement,
            CompositeMinimumRequiredPartCount = action.CompositeMinimumRequiredPartCount,
            CompositeMatchMode = action.CompositeMatchMode,
            CompositeAmbiguityPolicy = action.CompositeAmbiguityPolicy,
            CompositePartConsumptionMode = action.CompositePartConsumptionMode,
            CompositeResolvedTargetOutputTemplate = action.CompositeResolvedTargetOutputTemplate,
            ChildCommandForwardingMode = action.ChildCommandForwardingMode,
            SimilarChildDispatchMode = action.SimilarChildDispatchMode,
            OutcomeMessageMap = action.OutcomeMessageMap,
            OutcomeSoundEffectsMap = CommandAction.CloneOutcomeSoundEffectsMap(action.OutcomeSoundEffectsMap),
        };

        ActionPayloadAccessors.SetEchoMessage(clone, ActionPayloadAccessors.GetEchoMessage(action));

        var containerPayload = ActionPayloadAccessors.GetContainerTransfer(action);
        clone.TargetContainerId = containerPayload.TargetContainerId;

        if (action.ActionType == CommandActionType.BuildCompositeByTarget)
        {
            var payload = ActionPayloadAccessors.GetCompositeByTargetPayload(action);
            clone.CompositeTargetObjectId = payload.CompositeTargetObjectId;
            clone.CompositeRecipeId = payload.CompositeRecipeId;
            clone.CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            clone.CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            clone.CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            clone.CompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }
        else if (action.ActionType == CommandActionType.BuildCompositeByParts)
        {
            var payload = ActionPayloadAccessors.GetCompositeByPartsPayload(action);
            clone.CompositeTargetObjectId = payload.CompositeTargetObjectId;
            clone.CompositeRecipeId = payload.CompositeRecipeId;
            clone.CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            clone.CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            clone.CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            clone.CompositeMatchMode = payload.CompositeMatchMode;
            clone.CompositeAmbiguityPolicy = payload.CompositeAmbiguityPolicy;
            clone.CompositePartConsumptionMode = payload.CompositePartConsumptionMode;
            clone.CompositeResolvedTargetOutputTemplate = payload.CompositeResolvedTargetOutputTemplate;
        }
        else if (action.ActionType == CommandActionType.BreakCompositeItem)
        {
            var payload = ActionPayloadAccessors.GetBreakCompositePayload(action);
            clone.CompositeTargetObjectId = payload.CompositeTargetObjectId;
            clone.CompositeRecipeId = payload.CompositeRecipeId;
            clone.CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            clone.CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            clone.CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            clone.CompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }

        clone.Payload = action.CreatePayloadSnapshot();
        return clone;
    }

    private sealed class AreaNavigationSnapshot
    {
        public List<TraversalConnection> TraversalConnections { get; set; } = new();
        public List<AreaRoomPlacement> Placements { get; set; } = new();
    }

    private sealed class TraversalValidationIssueState
    {
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<string> Messages { get; } = new();
    }
}

