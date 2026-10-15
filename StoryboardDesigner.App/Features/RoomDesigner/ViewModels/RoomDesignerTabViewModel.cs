using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Storyboard.Shared.GameManager;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomDesignerTabViewModel : ViewModelBase
{
    private readonly record struct RoomChildMoveUndoEntry(Guid ObjectId, double PreviousX, double PreviousY, double NextX, double NextY);

    private readonly Action? _onEdited;
    private readonly Action<RoomDesignerTabViewModel>? _onDesignerPreferencesChanged;
    private readonly Func<string?>? _projectFilePathAccessor;
    private readonly Func<Guid, GameObject?>? _definitionResolver;
    private readonly Func<GameObject, bool>? _editRoomChildObjectImageAction;
    private readonly Area? _area;
    private readonly Stack<RoomChildMoveUndoEntry> _roomChildMoveUndoStack = new();
    private readonly HashSet<GameObject> _definitionSettingsSubscriptions = new();
    private int _designerCanvasWidth;
    private int _designerCanvasHeight;
    private int _roomGridCellSize;
    private bool _isGridVisible;
    private bool _isSnapToGridEnabled = true;
    private bool _isSavingRoomRenderOrder;
    private RoomDesignerImageSlotViewModel? _selectedDirectionSlot;
    private RoomDesignerPreviewObjectViewModel? _selectedRoomChildObject;
    private RoomDesignerRenderOrderItemViewModel? _selectedRoomRenderOrderItem;
    private bool _isUpdatingRenderOrderSelection;
    private string _roomChildObjectFilter = "All";
    private RoomDesignerPreviewPlacementComparisonMode _previewPlacementComparisonMode = RoomDesignerPreviewPlacementComparisonMode.SharedOnly;
    private GameDiagnosticsLevel _selectedRoomDesignerDiagnosticsLevel = GameDiagnosticsLevel.Medium;

    public RoomDesignerTabViewModel(
        Room room,
        int designerCanvasWidth,
        int designerCanvasHeight,
        int roomGridCellSize = 40,
        Action? onEdited = null,
        Func<string?>? projectFilePathAccessor = null,
        Func<Guid, GameObject?>? definitionResolver = null,
        Func<GameObject, bool>? editRoomChildObjectImageAction = null,
        bool isGridVisible = false,
        bool isSnapToGridEnabled = true,
        Action<RoomDesignerTabViewModel>? onDesignerPreferencesChanged = null,
        Area? area = null)
    {
        Room = room;
        _area = area ?? room.ParentScope as Area;
        Room.PropertyChanged += Room_OnPropertyChanged;
        _onEdited = onEdited;
        _onDesignerPreferencesChanged = onDesignerPreferencesChanged;
        _projectFilePathAccessor = projectFilePathAccessor;
        _definitionResolver = definitionResolver;
        _editRoomChildObjectImageAction = editRoomChildObjectImageAction;
        _designerCanvasWidth = designerCanvasWidth;
        _designerCanvasHeight = designerCanvasHeight;
        _roomGridCellSize = roomGridCellSize > 0 ? roomGridCellSize : 40;
        _isGridVisible = isGridVisible;
        _isSnapToGridEnabled = isSnapToGridEnabled;
        RoomImageSlots = new ObservableCollection<RoomDesignerImageSlotViewModel>();
        RoomChildObjects = new ObservableCollection<RoomDesignerPreviewObjectViewModel>();
        RoomRenderOrderItems = new ObservableCollection<RoomDesignerRenderOrderItemViewModel>();
        SelectRoomChildObjectCommand = new RelayCommandOfT<RoomDesignerPreviewObjectViewModel>(SelectRoomChildObject);
        MoveRoomChildObjectUpCommand = new RelayCommandOfT<object>(MoveRoomChildObjectUp, CanMoveRoomChildObjectUp);
        MoveRoomChildObjectDownCommand = new RelayCommandOfT<object>(MoveRoomChildObjectDown, CanMoveRoomChildObjectDown);
        EditRoomChildObjectImageCommand = new RelayCommandOfT<RoomDesignerPreviewObjectViewModel>(EditRoomChildObjectImage);
        ToggleDefaultSpawnPointCommand = new RelayCommand(ToggleDefaultSpawnPoint);
        BuildImageSlots();
        BuildRoomChildObjects();
    }

    public Room Room { get; }

    public ObservableCollection<RoomDesignerImageSlotViewModel> RoomImageSlots { get; }

    public ObservableCollection<RoomDesignerPreviewObjectViewModel> RoomChildObjects { get; }

    public ObservableCollection<RoomDesignerRenderOrderItemViewModel> RoomRenderOrderItems { get; }

    public IReadOnlyList<string> RoomChildObjectFilterOptions { get; } = ["All", "Visible", "Hidden"];

    public IReadOnlyList<Direction10> SpawnPointFacingOptions { get; } = Enum.GetValues<Direction10>();

    public bool HasDefaultSpawnPoint => Room.SpawnPoints.Any(static point => point.IsDefault);

    public string DefaultSpawnPointButtonText => HasDefaultSpawnPoint ? "Default: On" : "Default: Off";

    public bool IsSelectedSpawnPoint => SelectedRoomRenderOrderItem?.IsSpawnPoint == true;

    public double? SelectedSpawnPointX
    {
        get => SelectedRoomRenderOrderItem?.SpawnPoint?.X;
        set => UpdateSelectedSpawnCoordinate(value, isX: true);
    }

    public double? SelectedSpawnPointY
    {
        get => SelectedRoomRenderOrderItem?.SpawnPoint?.Y;
        set => UpdateSelectedSpawnCoordinate(value, isX: false);
    }

    public Direction10? SelectedSpawnPointFacingOverride
    {
        get => SelectedRoomRenderOrderItem?.SpawnPoint?.FacingOverride;
        set
        {
            if (SelectedRoomRenderOrderItem?.SpawnPoint is not { } point || point.FacingOverride == value) return;
            UpdateSpawnPoint(point, point with { FacingOverride = value });
            _onEdited?.Invoke();
        }
    }

    public IReadOnlyList<RoomDesignerPreviewPlacementComparisonMode> PreviewPlacementComparisonModeOptions { get; } =
    [
        RoomDesignerPreviewPlacementComparisonMode.SharedOnly
    ];

    public IReadOnlyList<GameDiagnosticsLevel> RoomDesignerDiagnosticsLevelOptions { get; } =
    [
        GameDiagnosticsLevel.None,
        GameDiagnosticsLevel.Low,
        GameDiagnosticsLevel.Medium,
        GameDiagnosticsLevel.High
    ];

    public ICommand SelectRoomChildObjectCommand { get; }
    public ICommand MoveRoomChildObjectUpCommand { get; }
    public ICommand MoveRoomChildObjectDownCommand { get; }
    public ICommand EditRoomChildObjectImageCommand { get; }
    public ICommand ToggleDefaultSpawnPointCommand { get; }

    public IReadOnlyList<RuntimeRoomImageDisplayMode> DisplayModeOptions { get; } =
    [
        RuntimeRoomImageDisplayMode.Independent,
        RuntimeRoomImageDisplayMode.Overlay
    ];

    public IEnumerable<RoomDesignerImageSlotViewModel> DirectionSlots => RoomImageSlots.Where(static slot =>
        slot.Entry.Slot is RoomImageSlot.North
            or RoomImageSlot.NorthEast
            or RoomImageSlot.East
            or RoomImageSlot.SouthEast
            or RoomImageSlot.South
            or RoomImageSlot.SouthWest
            or RoomImageSlot.West
            or RoomImageSlot.NorthWest
            or RoomImageSlot.Up
            or RoomImageSlot.Down);

    public RoomDesignerImageSlotViewModel? SelectedDirectionSlot
    {
        get => _selectedDirectionSlot;
        set
        {
            if (_selectedDirectionSlot == value)
            {
                return;
            }

            _selectedDirectionSlot = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDirectionThumbnailSource));
            OnPropertyChanged(nameof(ShowNoDirectionImagePlaceholder));
            OnPropertyChanged(nameof(SelectedDirectionHiddenIndicatorText));
            RaisePreviewPropertiesChanged();
        }
    }

    public RoomDesignerPreviewObjectViewModel? SelectedRoomChildObject
    {
        get => _selectedRoomChildObject;
        set
        {
            if (ReferenceEquals(_selectedRoomChildObject, value))
            {
                return;
            }

            if (_selectedRoomChildObject is not null)
            {
                _selectedRoomChildObject.IsSelected = false;
            }

            _selectedRoomChildObject = value;
            if (_selectedRoomChildObject is not null)
            {
                _selectedRoomChildObject.IsSelected = true;
            }

            OnPropertyChanged();
            if (!_isUpdatingRenderOrderSelection)
            {
                _selectedRoomRenderOrderItem = RoomRenderOrderItems.FirstOrDefault(item => ReferenceEquals(item.PreviewObject, value));
                OnPropertyChanged(nameof(SelectedRoomRenderOrderItem));
            }
            OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
            RaiseRoomChildCommandStates();
        }
    }

    public RoomDesignerRenderOrderItemViewModel? SelectedRoomRenderOrderItem
    {
        get => _selectedRoomRenderOrderItem;
        set
        {
            if (ReferenceEquals(_selectedRoomRenderOrderItem, value))
            {
                return;
            }

            _selectedRoomRenderOrderItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSelectedSpawnPoint));
            OnPropertyChanged(nameof(SelectedSpawnPointFacingOverride));
            OnPropertyChanged(nameof(SelectedSpawnPointX));
            OnPropertyChanged(nameof(SelectedSpawnPointY));
            _isUpdatingRenderOrderSelection = true;
            SelectedRoomChildObject = value?.PreviewObject;
            _isUpdatingRenderOrderSelection = false;
            RaiseRoomChildCommandStates();
        }
    }

    public RoomDesignerPreviewPlacementComparisonMode PreviewPlacementComparisonMode
    {
        get => _previewPlacementComparisonMode;
        set
        {
            if (_previewPlacementComparisonMode == value)
            {
                return;
            }

            _previewPlacementComparisonMode = value;
            foreach (var roomChild in RoomChildObjects)
            {
                roomChild.PlacementComparisonMode = _previewPlacementComparisonMode;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
            _onEdited?.Invoke();
        }
    }

    public GameDiagnosticsLevel SelectedRoomDesignerDiagnosticsLevel
    {
        get => _selectedRoomDesignerDiagnosticsLevel;
        set
        {
            if (_selectedRoomDesignerDiagnosticsLevel == value)
            {
                return;
            }

            _selectedRoomDesignerDiagnosticsLevel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
        }
    }

    public string RoomChildObjectFilter
    {
        get => _roomChildObjectFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "All" : value.Trim();
            if (string.Equals(_roomChildObjectFilter, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _roomChildObjectFilter = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredRoomChildObjects));
            OnPropertyChanged(nameof(FilteredRoomRenderOrderItems));
        }
    }

    public RuntimeRoomImageDisplayMode RoomDisplayMode
    {
        get => Room.RoomDisplayMode;
        set
        {
            if (Room.RoomDisplayMode == value)
            {
                return;
            }

            Room.RoomDisplayMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverlayMode));
            OnPropertyChanged(nameof(IsIndependentMode));
            RaisePreviewPropertiesChanged();
            _onEdited?.Invoke();
        }
    }

    public bool IsOverlayMode => RoomDisplayMode == RuntimeRoomImageDisplayMode.Overlay;

    public bool IsIndependentMode => RoomDisplayMode == RuntimeRoomImageDisplayMode.Independent;

    public object? SelectedDirectionThumbnailSource => SelectedDirectionSlot?.ThumbnailSource;

    public bool ShowNoDirectionImagePlaceholder => SelectedDirectionSlot?.ThumbnailSource is null;

    public string SelectedDirectionHiddenIndicatorText => SelectedDirectionSlot is { IsPreviewVisible: false }
        ? "Hidden in Preview"
        : string.Empty;

    public object? OverlayPreviewBaseImageSource => DefaultSlot?.ThumbnailSource;

    public IEnumerable<RoomDesignerImageSlotViewModel> OverlayPreviewSlots => DirectionSlots.Where(slot =>
        slot.IsPreviewVisible
        && slot.HasConfiguredImage)
        .OrderBy(slot => slot.OverlayRenderOrder)
        .ThenBy(slot => slot.Entry.Slot.ToString(), StringComparer.OrdinalIgnoreCase);

    public object? OverlayPreviewDirectionalImageSource => SelectedDirectionSlot is { IsPreviewVisible: true }
        ? SelectedDirectionSlot.VariantThumbnailSource
        : null;

    public double OverlayPreviewDirectionalOffsetX => SelectedDirectionSlot?.OverlayOffsetX ?? 0;

    public double OverlayPreviewDirectionalOffsetY => SelectedDirectionSlot?.OverlayOffsetY ?? 0;

    public double OverlayPreviewDirectionalRotation => SelectedDirectionSlot?.OverlayRotationDegrees ?? 0;

    public System.Windows.HorizontalAlignment OverlayPreviewDirectionalHorizontalAlignment =>
        SelectedDirectionSlot?.OverlayHorizontalAlignment ?? System.Windows.HorizontalAlignment.Left;

    public System.Windows.VerticalAlignment OverlayPreviewDirectionalVerticalAlignment =>
        SelectedDirectionSlot?.OverlayVerticalAlignment ?? System.Windows.VerticalAlignment.Top;

    public System.Windows.Point OverlayPreviewDirectionalRenderTransformOrigin =>
        SelectedDirectionSlot?.OverlayRenderTransformOrigin ?? new System.Windows.Point(0.5, 0.5);

    public object? IndependentPreviewImageSource => SelectedDirectionSlot is { IsPreviewVisible: true }
        ? SelectedDirectionSlot.ThumbnailSource
        : null;

    public bool ShowNoPreviewImagePlaceholder => IsOverlayMode
        ? OverlayPreviewBaseImageSource is null && !OverlayPreviewSlots.Any(slot => slot.VariantThumbnailSource is not null)
        : IndependentPreviewImageSource is null;

    public IReadOnlyList<RoomDesignerPreviewObjectViewModel> RenderableRoomChildObjects =>
        RoomChildObjects.Where(static item => item.IsRenderable).ToList();

    public IReadOnlyList<RoomDesignerPreviewObjectViewModel> FilteredRoomChildObjects => RoomChildObjectFilter switch
    {
        "Visible" => RoomChildObjects.Where(static item => item.IncludeInPreview).ToList(),
        "Hidden" => RoomChildObjects.Where(static item => !item.IncludeInPreview).ToList(),
        _ => RoomChildObjects.ToList()
    };

    public IReadOnlyList<RoomDesignerRenderOrderItemViewModel> FilteredRoomRenderOrderItems => RoomChildObjectFilter switch
    {
        "Visible" => RoomRenderOrderItems.Where(static item => item.IsSpawnPoint || item.IncludeInPreview).ToList(),
        "Hidden" => RoomRenderOrderItems.Where(static item => !item.IsSpawnPoint && !item.IncludeInPreview).ToList(),
        _ => RoomRenderOrderItems.ToList()
    };

    public IReadOnlyList<RoomDesignerRenderOrderItemViewModel> RenderableRoomRenderOrderItems =>
        RoomRenderOrderItems.Where(static item => item.IsRenderable).ToList();

    public IReadOnlyList<string> RoomDesignerDiagnosticsLines => BuildRoomDesignerDiagnosticsLines();

    public bool HasRenderableRoomChildObjects => RenderableRoomRenderOrderItems.Count > 0;

    public double SpawnPointPreviewX => Room.SpawnPoint.X;

    public double SpawnPointPreviewY => Room.SpawnPoint.Y;

    public string SpawnPointPreviewToolTip =>
        $"Room spawn point (drag to move). Render Z-order: {Room.SpawnPoint.RenderZOrder?.ToString(CultureInfo.InvariantCulture) ?? "default"}.";

    public int DesignerCanvasWidth
    {
        get => _designerCanvasWidth;
        set
        {
            if (_designerCanvasWidth == value)
            {
                return;
            }

            _designerCanvasWidth = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpawnPointPreviewX));
            OnPropertyChanged(nameof(EffectiveGridColumns));
            OnPropertyChanged(nameof(EffectiveGridRows));
            OnPropertyChanged(nameof(EffectiveGridSummary));
        }
    }

    public int DesignerCanvasHeight
    {
        get => _designerCanvasHeight;
        set
        {
            if (_designerCanvasHeight == value)
            {
                return;
            }

            _designerCanvasHeight = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpawnPointPreviewY));
            OnPropertyChanged(nameof(EffectiveGridColumns));
            OnPropertyChanged(nameof(EffectiveGridRows));
            OnPropertyChanged(nameof(EffectiveGridSummary));
        }
    }

    public int RoomGridCellSize
    {
        get => _roomGridCellSize;
        set
        {
            var sanitized = value > 0 ? value : 40;
            if (_roomGridCellSize == sanitized)
            {
                return;
            }

            _roomGridCellSize = sanitized;
            foreach (var roomChildObject in RoomChildObjects)
            {
                roomChildObject.UpdateGridCellSize(_roomGridCellSize);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveGridColumns));
            OnPropertyChanged(nameof(EffectiveGridRows));
            OnPropertyChanged(nameof(EffectiveGridSummary));
        }
    }

    public int EffectiveGridColumns => Math.Max(1, DesignerCanvasWidth / Math.Max(1, RoomGridCellSize));

    public int EffectiveGridRows => Math.Max(1, DesignerCanvasHeight / Math.Max(1, RoomGridCellSize));

    public string EffectiveGridSummary => $"{EffectiveGridColumns} columns x {EffectiveGridRows} rows";

    public bool IsGridVisible
    {
        get => _isGridVisible;
        set
        {
            if (_isGridVisible == value)
            {
                return;
            }

            _isGridVisible = value;
            OnPropertyChanged();
            _onDesignerPreferencesChanged?.Invoke(this);
        }
    }

    public bool IsSnapToGridEnabled
    {
        get => _isSnapToGridEnabled;
        set
        {
            if (_isSnapToGridEnabled == value)
            {
                return;
            }

            _isSnapToGridEnabled = value;
            OnPropertyChanged();
            _onDesignerPreferencesChanged?.Invoke(this);
        }
    }

    public bool TryMoveRoomSpawnPoint(RoomDesignerRenderOrderItemViewModel item, double x, double y)
    {
        if (item.SpawnPoint is null || !double.IsFinite(x) || !double.IsFinite(y)) return false;

        var nextX = Math.Clamp(x, 0, Math.Max(0, DesignerCanvasWidth));
        var nextY = Math.Clamp(y, 0, Math.Max(0, DesignerCanvasHeight));
        if (IsSnapToGridEnabled)
        {
            nextX = Math.Clamp(SnapCoordinate(nextX), 0, Math.Max(0, DesignerCanvasWidth));
            nextY = Math.Clamp(SnapCoordinate(nextY), 0, Math.Max(0, DesignerCanvasHeight));
        }

        UpdateSpawnPoint(item.SpawnPoint, item.SpawnPoint with { X = nextX, Y = nextY });
        _onEdited?.Invoke();
        return true;
    }

    private void Room_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(Room.SpawnPoints), StringComparison.Ordinal)
            && !string.Equals(e.PropertyName, nameof(Room.SpawnPoint), StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(SpawnPointPreviewX));
        OnPropertyChanged(nameof(SpawnPointPreviewY));
        OnPropertyChanged(nameof(SpawnPointPreviewToolTip));
        var spawnPointItem = RoomRenderOrderItems.FirstOrDefault(static item => item.IsSpawnPoint);
        if (_isSavingRoomRenderOrder)
        {
            spawnPointItem?.NotifySpawnPointChanged();
            return;
        }

        var selectedObjectId = _selectedRoomRenderOrderItem?.PreviewObject?.GameObject.ObjectId;
        var selectedSpawnPoint = _selectedRoomRenderOrderItem?.IsSpawnPoint == true;
        var selectedTraversalId = _selectedRoomRenderOrderItem?.SpawnPoint?.TraversalConnectionId;
        var selectedIsDefaultSpawn = _selectedRoomRenderOrderItem?.SpawnPoint?.IsDefault == true;
        BuildRoomRenderOrderItems();
        if (selectedSpawnPoint)
        {
            SelectedRoomRenderOrderItem = RoomRenderOrderItems.FirstOrDefault(item => item.IsSpawnPoint
                && item.SpawnPoint?.TraversalConnectionId == selectedTraversalId
                && item.SpawnPoint?.IsDefault == selectedIsDefaultSpawn);
        }
        else if (selectedObjectId.HasValue)
        {
            _selectedRoomRenderOrderItem = RoomRenderOrderItems.FirstOrDefault(item =>
                item.PreviewObject?.GameObject.ObjectId == selectedObjectId.Value);
            OnPropertyChanged(nameof(SelectedRoomRenderOrderItem));
        }

        OnPropertyChanged(nameof(RenderableRoomRenderOrderItems));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomRenderOrderItems));
        RaiseRoomChildCommandStates();
    }

    public IEnumerable<RoomDesignerImageSlotViewModel> OverlaySlots => RoomImageSlots.Where(slot =>
        slot.Entry.Slot != RoomImageSlot.Default
        && slot.HasConfiguredImage);

    public string Title => Room.Name;

    public string Description
    {
        get => Room.Description;
        set
        {
            if (Room.Description == value)
            {
                return;
            }

            Room.Description = value;
            OnPropertyChanged();
        }
    }

    public void BuildImageSlots()
    {
        RoomImageSlots.Clear();
        foreach (var entry in Room.Images.OrderBy(static i => i.Slot))
        {
            RoomImageSlots.Add(new RoomDesignerImageSlotViewModel(entry, OnSlotEdited, _projectFilePathAccessor, preferredSourceBucket: "rooms"));
        }

        OnPropertyChanged(nameof(DefaultSlot));
        OnPropertyChanged(nameof(NorthSlot));
        OnPropertyChanged(nameof(NorthEastSlot));
        OnPropertyChanged(nameof(EastSlot));
        OnPropertyChanged(nameof(SouthEastSlot));
        OnPropertyChanged(nameof(SouthSlot));
        OnPropertyChanged(nameof(SouthWestSlot));
        OnPropertyChanged(nameof(WestSlot));
        OnPropertyChanged(nameof(NorthWestSlot));
        OnPropertyChanged(nameof(UpSlot));
        OnPropertyChanged(nameof(DownSlot));
        OnPropertyChanged(nameof(OverlaySlots));
        OnPropertyChanged(nameof(DirectionSlots));

        SelectedDirectionSlot = DirectionSlots.FirstOrDefault();
        OnPropertyChanged(nameof(RoomDisplayMode));
        OnPropertyChanged(nameof(IsOverlayMode));
        OnPropertyChanged(nameof(IsIndependentMode));
        RaisePreviewPropertiesChanged();
    }

    public void BuildRoomChildObjects()
    {
        RefreshDefinitionSettingsSubscriptions();
        var selectedObjectId = SelectedRoomChildObject?.GameObject.ObjectId;
        var selectedSpawnPoint = _selectedRoomRenderOrderItem?.IsSpawnPoint == true;

        RoomChildObjects.Clear();

        var totalObjectCount = CountRoomObjectTree(Room.GameObjects);
        var drawOrderStride = totalObjectCount + 1;
        for (var index = 0; index < Room.GameObjects.Count; index++)
        {
            var gameObject = Room.GameObjects[index];
            var rootDrawOrder = (Room.GameObjects.Count - index) * drawOrderStride;
            var preview = new RoomDesignerPreviewObjectViewModel(gameObject, OnRoomChildObjectEdited, _projectFilePathAccessor, _definitionResolver)
            {
                DrawOrderZIndex = gameObject.RenderZOrder != 0 ? gameObject.RenderZOrder : rootDrawOrder
            };
            preview.PlacementComparisonMode = _previewPlacementComparisonMode;
            preview.UpdateGridCellSize(_roomGridCellSize);

            RoomChildObjects.Add(preview);
            AddVisibleContainedObjectPreviews(gameObject, rootDrawOrder, 1);
        }

        // Once the author has explicitly arranged the visible room objects, preserve that
        // flat draw order across rebuilds even though the objects remain in their original
        // containment hierarchy.
        if (RoomChildObjects.Count > 0 && RoomChildObjects.All(item => item.GameObject.RenderZOrder != 0))
        {
            var orderedPreviews = RoomChildObjects
                .OrderByDescending(item => item.GameObject.RenderZOrder)
                .ToList();
            RoomChildObjects.Clear();
            foreach (var preview in orderedPreviews)
            {
                preview.DrawOrderZIndex = preview.GameObject.RenderZOrder;
                RoomChildObjects.Add(preview);
            }
        }

        BuildRoomRenderOrderItems();

        SelectedRoomChildObject = selectedObjectId.HasValue
            ? RoomChildObjects.FirstOrDefault(item => item.GameObject.ObjectId == selectedObjectId.Value)
            : selectedSpawnPoint ? null : RoomChildObjects.FirstOrDefault();
        if (selectedSpawnPoint)
        {
            SelectedRoomRenderOrderItem = RoomRenderOrderItems.FirstOrDefault(static item => item.IsSpawnPoint);
        }

        OnPropertyChanged(nameof(RenderableRoomChildObjects));
        OnPropertyChanged(nameof(RenderableRoomRenderOrderItems));
        OnPropertyChanged(nameof(HasRenderableRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomRenderOrderItems));
        OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
        RaiseRoomChildCommandStates();
    }

    private void BuildRoomRenderOrderItems()
    {
        foreach (var existingItem in RoomRenderOrderItems)
        {
            existingItem.Detach();
        }

        RoomRenderOrderItems.Clear();
        foreach (var preview in RoomChildObjects)
        {
            RoomRenderOrderItems.Add(new RoomDesignerRenderOrderItemViewModel(Room, preview, preview.DrawOrderZIndex));
        }

        var defaultSpawnPointOrder = RoomRenderOrderItems.Count == 0
            ? 1
            : RoomRenderOrderItems.Max(static item => item.DrawOrderZIndex) + 1;
        var spawnPoints = Room.SpawnPoints;
        for (var index = 0; index < spawnPoints.Count; index++)
        {
            var point = spawnPoints[index];
            var label = point.IsDefault ? "Player Spawn Point (Default)" : "Player Spawn Point";
            var markerText = "•";
            if (point.TraversalConnectionId is Guid traversalId)
            {
                var traversal = _area?.TraversalConnections.FirstOrDefault(connection => connection.TraversalConnectionId == traversalId);
                var sourceName = traversal is null
                    ? "unknown traversal"
                    : _area!.Rooms.FirstOrDefault(room => room.Id == (traversal.RoomAId == Room.Id ? traversal.RoomBId : traversal.RoomAId))?.Name ?? "unknown room";
                var arrival = traversal is null ? "unknown direction" : RoomSpawnPointAuthoringService.DirectionFromRoom(traversal, Room.Id).ToString();
                label += $" — {arrival} toward {sourceName} (arrives in {Room.Name})";
                markerText = traversal is null
                    ? "?"
                    : GetSpawnPointDirectionMarker(RoomSpawnPointAuthoringService.DirectionFromRoom(traversal, Room.Id));
            }
            var spawnOrder = point.RenderZOrder ?? defaultSpawnPointOrder + index;
            RoomRenderOrderItems.Add(new RoomDesignerRenderOrderItemViewModel(Room, point, label, markerText, spawnOrder));
        }

        var orderedItems = RoomRenderOrderItems
            .OrderByDescending(static item => item.DrawOrderZIndex)
            .ToList();
        RoomRenderOrderItems.Clear();
        foreach (var item in orderedItems)
        {
            RoomRenderOrderItems.Add(item);
        }
        OnPropertyChanged(nameof(HasDefaultSpawnPoint));
        OnPropertyChanged(nameof(DefaultSpawnPointButtonText));
    }

    private void ToggleDefaultSpawnPoint()
    {
        if (HasDefaultSpawnPoint)
        {
            Room.SetDefaultSpawnPoint(null);
        }
        else
        {
            Room.SetDefaultSpawnPoint(new RoomSpawnPoint(
                DesignerCanvasWidth / 2d,
                DesignerCanvasHeight / 2d,
                GetTopSpawnPointRenderOrder(),
                IsDefault: true));
        }
        _onEdited?.Invoke();
    }

    private int GetTopSpawnPointRenderOrder() => RoomRenderOrderItems.Count == 0
        ? 1
        : RoomRenderOrderItems.Max(static item => item.DrawOrderZIndex) + 1;

    private static string GetSpawnPointDirectionMarker(Direction10 direction) => direction switch
    {
        Direction10.North => "N",
        Direction10.NorthEast => "NE",
        Direction10.East => "E",
        Direction10.SouthEast => "SE",
        Direction10.South => "S",
        Direction10.SouthWest => "SW",
        Direction10.West => "W",
        Direction10.NorthWest => "NW",
        Direction10.Up => "U",
        Direction10.Down => "D",
        _ => "?"
    };

    private void UpdateSpawnPoint(RoomSpawnPoint previous, RoomSpawnPoint next)
    {
        if (previous.X != next.X || previous.Y != next.Y)
            next = next with { IsAutoGeneratedPosition = false, GeneratedPositionX = null, GeneratedPositionY = null };
        var points = Room.SpawnPoints.ToList();
        var index = points.FindIndex(point => point == previous);
        if (index < 0) return;
        points[index] = next;
        Room.SpawnPoints = points;
    }

    private void UpdateSelectedSpawnCoordinate(double? value, bool isX)
    {
        if (value is not double coordinate || !double.IsFinite(coordinate)
            || SelectedRoomRenderOrderItem?.SpawnPoint is not { } point) return;
        UpdateSpawnPoint(point, point with { X = isX ? Math.Clamp(coordinate, 0, DesignerCanvasWidth) : point.X,
            Y = isX ? point.Y : Math.Clamp(coordinate, 0, DesignerCanvasHeight) });
        _onEdited?.Invoke();
    }

    private void RefreshDefinitionSettingsSubscriptions()
    {
        var activeSources = new HashSet<GameObject>();
        void Visit(GameObject current)
        {
            activeSources.Add(current.ResolveEffectiveDefinitionOwnedSource(_definitionResolver));
            foreach (var child in current.ContainedObjects)
            {
                Visit(child);
            }
        }

        foreach (var root in Room.GameObjects)
        {
            Visit(root);
        }

        foreach (var obsolete in _definitionSettingsSubscriptions.Except(activeSources).ToList())
        {
            obsolete.PropertyChanged -= OnDefinitionSettingsPropertyChanged;
            _definitionSettingsSubscriptions.Remove(obsolete);
        }

        foreach (var source in activeSources.Except(_definitionSettingsSubscriptions))
        {
            source.PropertyChanged += OnDefinitionSettingsPropertyChanged;
            _definitionSettingsSubscriptions.Add(source);
        }
    }

    private void OnDefinitionSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(GameObject.HideContainedObjects), StringComparison.Ordinal))
        {
            BuildRoomChildObjects();
        }
    }

    private void AddVisibleContainedObjectPreviews(GameObject parent, int rootDrawOrder, int depth)
    {
        foreach (var child in parent.ContainedObjects)
        {
            var isVisibleWhileContained = child.IsInventoriable && child.ShowWhenContained.HasValue
                ? child.ShowWhenContained.Value
                : !parent.ResolveEffectiveDefinitionOwnedSource(_definitionResolver).HideContainedObjects;
            if (!isVisibleWhileContained)
            {
                continue;
            }

            var preview = new RoomDesignerPreviewObjectViewModel(child, OnRoomChildObjectEdited, _projectFilePathAccessor, _definitionResolver)
            {
                // Contained parts start behind their parent. The list controls can move
                // them in front later when the authored scene requires it.
                DrawOrderZIndex = child.RenderZOrder != 0 ? child.RenderZOrder : rootDrawOrder - depth
            };
            preview.PlacementComparisonMode = _previewPlacementComparisonMode;
            preview.UpdateGridCellSize(_roomGridCellSize);
            RoomChildObjects.Add(preview);

            AddVisibleContainedObjectPreviews(child, rootDrawOrder, depth + 1);
        }
    }

    private static int CountRoomObjectTree(IEnumerable<GameObject> objects)
    {
        var count = 0;
        foreach (var gameObject in objects)
        {
            count++;
            count += CountRoomObjectTree(gameObject.ContainedObjects);
        }

        return count;
    }

    private void OnSlotEdited()
    {
        _onEdited?.Invoke();
        OnPropertyChanged(nameof(OverlaySlots));
        OnPropertyChanged(nameof(OverlayPreviewSlots));
        OnPropertyChanged(nameof(SelectedDirectionThumbnailSource));
        OnPropertyChanged(nameof(ShowNoDirectionImagePlaceholder));
        OnPropertyChanged(nameof(SelectedDirectionHiddenIndicatorText));
        RaisePreviewPropertiesChanged();
    }

    private void OnRoomChildObjectEdited()
    {
        _onEdited?.Invoke();
        OnPropertyChanged(nameof(RenderableRoomChildObjects));
        OnPropertyChanged(nameof(RenderableRoomRenderOrderItems));
        OnPropertyChanged(nameof(HasRenderableRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomRenderOrderItems));
        OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
        RaiseRoomChildCommandStates();
    }

    public bool NudgeSelectedRoomChildObject(
        double deltaX,
        double deltaY,
        double previewWidth,
        double previewHeight,
        double objectWidth,
        double objectHeight)
    {
        var selected = SelectedRoomChildObject;
        if (selected is null || !selected.IsRenderable)
        {
            return false;
        }

        var maxX = Math.Max(0, previewWidth - objectWidth);
        var maxY = Math.Max(0, previewHeight - objectHeight);

        var nextX = Math.Clamp(selected.PositionX + deltaX, 0, maxX);
        var nextY = Math.Clamp(selected.PositionY + deltaY, 0, maxY);
        nextX = Math.Clamp(SnapCoordinate(nextX), 0, maxX);
        nextY = Math.Clamp(SnapCoordinate(nextY), 0, maxY);

        var changed = Math.Abs(nextX - selected.PositionX) > 0.0001
                      || Math.Abs(nextY - selected.PositionY) > 0.0001;
        if (!changed)
        {
            return false;
        }

        return TryCommitRoomChildObjectMove(selected, selected.PositionX, selected.PositionY, nextX, nextY);
    }

    public bool TryCommitRoomChildObjectMove(
        RoomDesignerPreviewObjectViewModel? selected,
        double previousX,
        double previousY,
        double nextX,
        double nextY)
    {
        if (selected is null)
        {
            return false;
        }

        var normalizedPreviousX = double.IsFinite(previousX) ? previousX : 0;
        var normalizedPreviousY = double.IsFinite(previousY) ? previousY : 0;
        var normalizedNextX = double.IsFinite(nextX) ? nextX : 0;
        var normalizedNextY = double.IsFinite(nextY) ? nextY : 0;

        var changed = Math.Abs(normalizedNextX - normalizedPreviousX) > 0.0001
                      || Math.Abs(normalizedNextY - normalizedPreviousY) > 0.0001;
        if (!changed)
        {
            return false;
        }

        selected.PositionX = normalizedNextX;
        selected.PositionY = normalizedNextY;
        _roomChildMoveUndoStack.Push(new RoomChildMoveUndoEntry(selected.GameObject.ObjectId, normalizedPreviousX, normalizedPreviousY, normalizedNextX, normalizedNextY));
        return true;
    }

    public double SnapCoordinate(double value)
    {
        var normalized = double.IsFinite(value) ? value : 0;
        if (!IsSnapToGridEnabled)
        {
            return normalized;
        }

        var cellSize = Math.Max(1, RoomGridCellSize);
        return Math.Round(normalized / cellSize) * cellSize;
    }

    public double GetKeyboardNudgeStep(bool accelerated)
    {
        if (IsSnapToGridEnabled)
        {
            var baseStep = Math.Max(1, RoomGridCellSize);
            return accelerated ? baseStep * 5 : baseStep;
        }

        return accelerated ? 10 : 1;
    }

    public bool TryUndoLastRoomChildMove()
    {
        if (_roomChildMoveUndoStack.Count == 0)
        {
            return false;
        }

        var entry = _roomChildMoveUndoStack.Pop();
        var selected = RoomChildObjects.FirstOrDefault(item => item.GameObject.ObjectId == entry.ObjectId);
        if (selected is null)
        {
            return false;
        }

        SelectedRoomChildObject = selected;
        selected.PositionX = entry.PreviousX;
        selected.PositionY = entry.PreviousY;
        return true;
    }

    public RoomDesignerImageSlotViewModel? DefaultSlot => FindSlot(RoomImageSlot.Default);
    public RoomDesignerImageSlotViewModel? NorthSlot => FindSlot(RoomImageSlot.North);
    public RoomDesignerImageSlotViewModel? NorthEastSlot => FindSlot(RoomImageSlot.NorthEast);
    public RoomDesignerImageSlotViewModel? EastSlot => FindSlot(RoomImageSlot.East);
    public RoomDesignerImageSlotViewModel? SouthEastSlot => FindSlot(RoomImageSlot.SouthEast);
    public RoomDesignerImageSlotViewModel? SouthSlot => FindSlot(RoomImageSlot.South);
    public RoomDesignerImageSlotViewModel? SouthWestSlot => FindSlot(RoomImageSlot.SouthWest);
    public RoomDesignerImageSlotViewModel? WestSlot => FindSlot(RoomImageSlot.West);
    public RoomDesignerImageSlotViewModel? NorthWestSlot => FindSlot(RoomImageSlot.NorthWest);
    public RoomDesignerImageSlotViewModel? UpSlot => FindSlot(RoomImageSlot.Up);
    public RoomDesignerImageSlotViewModel? DownSlot => FindSlot(RoomImageSlot.Down);

    private RoomDesignerImageSlotViewModel? FindSlot(RoomImageSlot slot)
    {
        return RoomImageSlots.FirstOrDefault(s => s.Entry.Slot == slot);
    }

    private void SelectRoomChildObject(RoomDesignerPreviewObjectViewModel? preview)
    {
        if (preview is null)
        {
            return;
        }

        SelectedRoomChildObject = preview;
        RaiseRoomChildCommandStates();
    }

    private RoomDesignerRenderOrderItemViewModel? ResolveRenderOrderItem(object? value) => value switch
    {
        RoomDesignerRenderOrderItemViewModel item => item,
        RoomDesignerPreviewObjectViewModel preview => RoomRenderOrderItems.FirstOrDefault(item => ReferenceEquals(item.PreviewObject, preview)),
        _ => null
    };

    private bool CanMoveRoomChildObjectUp(object? value)
    {
        var item = ResolveRenderOrderItem(value);
        return item is not null && RoomRenderOrderItems.IndexOf(item) > 0;
    }

    private bool CanMoveRoomChildObjectDown(object? value)
    {
        var item = ResolveRenderOrderItem(value);
        return item is not null
               && RoomRenderOrderItems.IndexOf(item) >= 0
               && RoomRenderOrderItems.IndexOf(item) < RoomRenderOrderItems.Count - 1;
    }

    private void MoveRoomChildObjectUp(object? value)
    {
        var item = ResolveRenderOrderItem(value);
        if (item is null)
        {
            return;
        }

        var currentIndex = RoomRenderOrderItems.IndexOf(item);
        if (currentIndex <= 0)
        {
            return;
        }

        RoomRenderOrderItems.Move(currentIndex, currentIndex - 1);
        SaveRoomPreviewDrawOrder();
        SelectedRoomRenderOrderItem = item;
        OnPropertyChanged(nameof(RenderableRoomRenderOrderItems));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomRenderOrderItems));
        RaiseRoomChildCommandStates();
        _onEdited?.Invoke();
    }

    private void MoveRoomChildObjectDown(object? value)
    {
        var item = ResolveRenderOrderItem(value);
        if (item is null)
        {
            return;
        }

        var currentIndex = RoomRenderOrderItems.IndexOf(item);
        if (currentIndex < 0 || currentIndex >= RoomRenderOrderItems.Count - 1)
        {
            return;
        }

        RoomRenderOrderItems.Move(currentIndex, currentIndex + 1);
        SaveRoomPreviewDrawOrder();
        SelectedRoomRenderOrderItem = item;
        OnPropertyChanged(nameof(RenderableRoomRenderOrderItems));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomRenderOrderItems));
        RaiseRoomChildCommandStates();
        _onEdited?.Invoke();
    }

    private void SaveRoomPreviewDrawOrder()
    {
        _isSavingRoomRenderOrder = true;
        try
        {
            var count = RoomRenderOrderItems.Count;
            for (var index = 0; index < count; index++)
            {
                var item = RoomRenderOrderItems[index];
                var zOrder = count - index;
                item.DrawOrderZIndex = zOrder;
                if (item.IsSpawnPoint)
                {
                    if (item.SpawnPoint is { } point)
                        UpdateSpawnPoint(point, point with { RenderZOrder = zOrder });
                }
                else if (item.PreviewObject is { } preview)
                {
                    preview.GameObject.RenderZOrder = zOrder;
                }
            }

            var orderedObjectPreviews = RoomRenderOrderItems
                .Where(static item => item.PreviewObject is not null)
                .Select(static item => item.PreviewObject!)
                .ToList();
            RoomChildObjects.Clear();
            foreach (var preview in orderedObjectPreviews)
            {
                RoomChildObjects.Add(preview);
            }
        }
        finally
        {
            _isSavingRoomRenderOrder = false;
        }

        OnPropertyChanged(nameof(RenderableRoomChildObjects));
        OnPropertyChanged(nameof(RenderableRoomRenderOrderItems));
        OnPropertyChanged(nameof(HasRenderableRoomChildObjects));
    }

    private void EditRoomChildObjectImage(RoomDesignerPreviewObjectViewModel? preview)
    {
        if (preview is null || _editRoomChildObjectImageAction is null)
        {
            return;
        }

        if (_editRoomChildObjectImageAction(preview.GameObject))
        {
            OnRoomChildObjectEdited();
        }
    }

    private void RaiseRoomChildCommandStates()
    {
        if (MoveRoomChildObjectUpCommand is RelayCommandOfT<object> moveUp)
        {
            moveUp.RaiseCanExecuteChanged();
        }

        if (MoveRoomChildObjectDownCommand is RelayCommandOfT<object> moveDown)
        {
            moveDown.RaiseCanExecuteChanged();
        }
    }

    private void RaisePreviewPropertiesChanged()
    {
        OnPropertyChanged(nameof(OverlayPreviewBaseImageSource));
        OnPropertyChanged(nameof(OverlayPreviewSlots));
        OnPropertyChanged(nameof(OverlayPreviewDirectionalImageSource));
        OnPropertyChanged(nameof(OverlayPreviewDirectionalOffsetX));
        OnPropertyChanged(nameof(OverlayPreviewDirectionalOffsetY));
        OnPropertyChanged(nameof(OverlayPreviewDirectionalRotation));
        OnPropertyChanged(nameof(OverlayPreviewDirectionalHorizontalAlignment));
        OnPropertyChanged(nameof(OverlayPreviewDirectionalVerticalAlignment));
        OnPropertyChanged(nameof(OverlayPreviewDirectionalRenderTransformOrigin));
        OnPropertyChanged(nameof(IndependentPreviewImageSource));
        OnPropertyChanged(nameof(ShowNoPreviewImagePlaceholder));
        OnPropertyChanged(nameof(RenderableRoomChildObjects));
        OnPropertyChanged(nameof(RenderableRoomRenderOrderItems));
        OnPropertyChanged(nameof(HasRenderableRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomRenderOrderItems));
        OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
    }

    private IReadOnlyList<string> BuildRoomDesignerDiagnosticsLines()
    {
        if (SelectedRoomDesignerDiagnosticsLevel == GameDiagnosticsLevel.None)
        {
            return ["Diagnostics disabled."];
        }

        var selected = SelectedRoomChildObject;
        var lines = new List<string>
        {
            selected is null
                ? $"level={SelectedRoomDesignerDiagnosticsLevel}; mode={PreviewPlacementComparisonMode}; selected=none"
                : $"level={SelectedRoomDesignerDiagnosticsLevel}; mode={PreviewPlacementComparisonMode}; selected='{selected.DisplayName}'"
        };

        if (selected is not null)
        {
            lines.Add(selected.PlacementComparisonSnapshot);
        }

        lines.Add($"canvas={DesignerCanvasWidth}x{DesignerCanvasHeight}; roomDisplayMode={RoomDisplayMode}; gridCell={RoomGridCellSize}; overlayVisibleSlots={OverlayPreviewSlots.Count()}");

        var selectedDirection = SelectedDirectionSlot;
        if (selectedDirection is null)
        {
            lines.Add("selectedDirection=none");
        }
        else
        {
            var origin = selectedDirection.OverlayRenderTransformOrigin;
            lines.Add(
                $"selectedDirection={selectedDirection.Entry.Slot}; visible={selectedDirection.IsPreviewVisible}; hasImage={selectedDirection.HasConfiguredImage}; align=({selectedDirection.OverlayHorizontalAlignment},{selectedDirection.OverlayVerticalAlignment}); origin=({Format(origin.X)},{Format(origin.Y)}); offset=({Format(selectedDirection.OverlayOffsetX)},{Format(selectedDirection.OverlayOffsetY)}); rot={Format(selectedDirection.OverlayRotationDegrees)}; order={selectedDirection.OverlayRenderOrder}");
        }

        if (SelectedRoomDesignerDiagnosticsLevel >= GameDiagnosticsLevel.Medium)
        {
            if (selected is null)
            {
                lines.Add("No selected room object.");
            }
            else
            {
                lines.Add(
                    $"position=({Format(selected.PositionX)},{Format(selected.PositionY)}); roomRot={Format(selected.RoomRotationDegrees)}; imageRot={Format(selected.ImageRotationDegrees)}; scale={Format(selected.EffectiveImageScale)}");
                lines.Add(
                    $"footprintPx=({Format(selected.FootprintWidthPixels)}x{Format(selected.FootprintHeightPixels)}); baseIconPx=({Format(selected.PreviewBaseIconWidthPixels)}x{Format(selected.PreviewBaseIconHeightPixels)})");
            }

            foreach (var slot in OverlayPreviewSlots)
            {
                var origin = slot.OverlayRenderTransformOrigin;
                lines.Add(
                    $"overlaySlot={slot.Entry.Slot}; align=({slot.OverlayHorizontalAlignment},{slot.OverlayVerticalAlignment}); origin=({Format(origin.X)},{Format(origin.Y)}); offset=({Format(slot.OverlayOffsetX)},{Format(slot.OverlayOffsetY)}); rot={Format(slot.OverlayRotationDegrees)}; order={slot.OverlayRenderOrder}; hasImage={slot.HasConfiguredImage}");
            }
        }

        if (SelectedRoomDesignerDiagnosticsLevel >= GameDiagnosticsLevel.High && selected is not null)
        {
            lines.Add(
                $"selectedOffset=({Format(selected.ImageLocalAlignmentOffsetX)},{Format(selected.ImageLocalAlignmentOffsetY)}); delta=({Format(selected.PlacementComparisonDeltaX)},{Format(selected.PlacementComparisonDeltaY)})");
            lines.Add(
                $"renderable={selected.IsRenderable}; includeInPreview={selected.IncludeInPreview}; z={selected.DrawOrderZIndex}");

            var renderables = RenderableRoomChildObjects;
            lines.Add($"roomRenderableCount={renderables.Count}");
            foreach (var roomChild in renderables)
            {
                lines.Add($"object='{roomChild.DisplayName}'; {roomChild.PlacementComparisonSnapshot}");
            }
        }

        return lines;
    }

    private static string Format(double value)
    {
        if (!double.IsFinite(value))
        {
            return "n/a";
        }

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

