using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Storyboard.Shared.GameManager;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomDesignerTabViewModel : ViewModelBase
{
    private readonly record struct RoomChildMoveUndoEntry(Guid ObjectId, double PreviousX, double PreviousY, double NextX, double NextY);

    private readonly Action? _onEdited;
    private readonly Action<RoomDesignerTabViewModel>? _onDesignerPreferencesChanged;
    private readonly Func<string?>? _projectFilePathAccessor;
    private readonly Func<Guid, GameObject?>? _definitionResolver;
    private readonly Func<GameObject, bool>? _editRoomChildObjectImageAction;
    private readonly Stack<RoomChildMoveUndoEntry> _roomChildMoveUndoStack = new();
    private int _designerCanvasWidth;
    private int _designerCanvasHeight;
    private int _roomGridCellSize;
    private bool _isGridVisible;
    private bool _isSnapToGridEnabled = true;
    private RoomDesignerImageSlotViewModel? _selectedDirectionSlot;
    private RoomDesignerPreviewObjectViewModel? _selectedRoomChildObject;
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
        Action<RoomDesignerTabViewModel>? onDesignerPreferencesChanged = null)
    {
        Room = room;
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
        SelectRoomChildObjectCommand = new RelayCommandOfT<RoomDesignerPreviewObjectViewModel>(SelectRoomChildObject);
        MoveRoomChildObjectUpCommand = new RelayCommandOfT<RoomDesignerPreviewObjectViewModel>(MoveRoomChildObjectUp, CanMoveRoomChildObjectUp);
        MoveRoomChildObjectDownCommand = new RelayCommandOfT<RoomDesignerPreviewObjectViewModel>(MoveRoomChildObjectDown, CanMoveRoomChildObjectDown);
        EditRoomChildObjectImageCommand = new RelayCommandOfT<RoomDesignerPreviewObjectViewModel>(EditRoomChildObjectImage);
        BuildImageSlots();
        BuildRoomChildObjects();
    }

    public Room Room { get; }

    public ObservableCollection<RoomDesignerImageSlotViewModel> RoomImageSlots { get; }

    public ObservableCollection<RoomDesignerPreviewObjectViewModel> RoomChildObjects { get; }

    public IReadOnlyList<string> RoomChildObjectFilterOptions { get; } = ["All", "Visible", "Hidden"];

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
            OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
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

    public IReadOnlyList<string> RoomDesignerDiagnosticsLines => BuildRoomDesignerDiagnosticsLines();

    public bool HasRenderableRoomChildObjects => RenderableRoomChildObjects.Count > 0;

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
        var selectedObjectId = SelectedRoomChildObject?.GameObject.ObjectId;

        RoomChildObjects.Clear();

        for (var index = 0; index < Room.GameObjects.Count; index++)
        {
            var gameObject = Room.GameObjects[index];
            var preview = new RoomDesignerPreviewObjectViewModel(gameObject, OnRoomChildObjectEdited, _projectFilePathAccessor, _definitionResolver)
            {
                DrawOrderZIndex = Room.GameObjects.Count - index
            };
            preview.PlacementComparisonMode = _previewPlacementComparisonMode;
            preview.UpdateGridCellSize(_roomGridCellSize);

            RoomChildObjects.Add(preview);
        }

        SelectedRoomChildObject = selectedObjectId.HasValue
            ? RoomChildObjects.FirstOrDefault(item => item.GameObject.ObjectId == selectedObjectId.Value)
            : RoomChildObjects.FirstOrDefault();

        OnPropertyChanged(nameof(RenderableRoomChildObjects));
        OnPropertyChanged(nameof(HasRenderableRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
        OnPropertyChanged(nameof(RoomDesignerDiagnosticsLines));
        RaiseRoomChildCommandStates();
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
        OnPropertyChanged(nameof(HasRenderableRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
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

    private bool CanMoveRoomChildObjectUp(RoomDesignerPreviewObjectViewModel? preview)
    {
        if (preview is null)
        {
            return false;
        }

        return Room.GameObjects.IndexOf(preview.GameObject) > 0;
    }

    private bool CanMoveRoomChildObjectDown(RoomDesignerPreviewObjectViewModel? preview)
    {
        if (preview is null)
        {
            return false;
        }

        var index = Room.GameObjects.IndexOf(preview.GameObject);
        return index >= 0 && index < Room.GameObjects.Count - 1;
    }

    private void MoveRoomChildObjectUp(RoomDesignerPreviewObjectViewModel? preview)
    {
        if (preview is null)
        {
            return;
        }

        var currentIndex = Room.GameObjects.IndexOf(preview.GameObject);
        if (currentIndex <= 0)
        {
            return;
        }

        Room.GameObjects.RemoveAt(currentIndex);
        Room.GameObjects.Insert(currentIndex - 1, preview.GameObject);
        BuildRoomChildObjects();
        SelectedRoomChildObject = RoomChildObjects.FirstOrDefault(item => item.GameObject == preview.GameObject);
        _onEdited?.Invoke();
    }

    private void MoveRoomChildObjectDown(RoomDesignerPreviewObjectViewModel? preview)
    {
        if (preview is null)
        {
            return;
        }

        var currentIndex = Room.GameObjects.IndexOf(preview.GameObject);
        if (currentIndex < 0 || currentIndex >= Room.GameObjects.Count - 1)
        {
            return;
        }

        Room.GameObjects.RemoveAt(currentIndex);
        Room.GameObjects.Insert(currentIndex + 1, preview.GameObject);
        BuildRoomChildObjects();
        SelectedRoomChildObject = RoomChildObjects.FirstOrDefault(item => item.GameObject == preview.GameObject);
        _onEdited?.Invoke();
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
        if (MoveRoomChildObjectUpCommand is RelayCommandOfT<RoomDesignerPreviewObjectViewModel> moveUp)
        {
            moveUp.RaiseCanExecuteChanged();
        }

        if (MoveRoomChildObjectDownCommand is RelayCommandOfT<RoomDesignerPreviewObjectViewModel> moveDown)
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
        OnPropertyChanged(nameof(HasRenderableRoomChildObjects));
        OnPropertyChanged(nameof(FilteredRoomChildObjects));
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

