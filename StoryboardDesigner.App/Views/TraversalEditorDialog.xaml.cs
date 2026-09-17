using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class TraversalEditorDialog : Window
{
    private readonly PresentationEffectsCatalogService _presentationEffectsCatalogService = new();
    private IReadOnlyDictionary<Guid, Room> _roomsById = new Dictionary<Guid, Room>();
    private IReadOnlyList<Room> _roomList = Array.Empty<Room>();
    private readonly Dictionary<Guid, Dictionary<Direction10, Guid>> _guidedDestinationsByRoom = new();
    private IReadOnlyList<Direction10> _allowedDirections = DirectionValues.DefaultTraversalDirections;
    private bool _suppressSelectionHandlers;
    private bool _isInitializingDialog;

    private sealed class DoorOption
    {
        public Guid? ObjectId { get; init; }
        public required string Label { get; init; }
    }

    private sealed class PresentationEffectOption
    {
        public string? EffectKey { get; init; }
        public required string Label { get; init; }
    }

    public TraversalEditorDialog(
        TraversalConnection connection,
        IReadOnlyCollection<Room>? rooms,
        IReadOnlyCollection<Direction10>? allowedDirections = null)
    {
        InitializeWithArea(connection, rooms, null, allowedDirections);
    }

    public TraversalEditorDialog(
        TraversalConnection connection,
        IReadOnlyCollection<Room>? rooms,
        Area? area,
        IReadOnlyCollection<Direction10>? allowedDirections = null)
    {
        InitializeWithArea(connection, rooms, area, allowedDirections);
    }

    private void InitializeWithArea(
        TraversalConnection connection,
        IReadOnlyCollection<Room>? rooms,
        Area? area,
        IReadOnlyCollection<Direction10>? allowedDirections)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _isInitializingDialog = true;
        try
        {
            InitializeComponent();

            _allowedDirections = (allowedDirections ?? DirectionValues.DefaultTraversalDirections)
                .Distinct()
                .ToList();
            if (_allowedDirections.Count == 0)
            {
                _allowedDirections = DirectionValues.DefaultTraversalDirections;
            }

            var stateFromA = connection.TraversalStateFromA ??= new TraversalLegState();
            var stateFromB = connection.TraversalStateFromB ??= new TraversalLegState();
            IReadOnlyCollection<Room> safeRooms = rooms ?? area?.Rooms ?? new List<Room>();

            _roomList = safeRooms
                .Where(room => room is not null)
                .GroupBy(room => room.Id)
                .Select(group => group.First())
                .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _roomsById = _roomList.ToDictionary(room => room.Id);
            BuildGuidedDestinations(area);
            var configuredEffects = BuildRoomTransitionEffectOptions(connection.PresentationEffectKey);

            RoomAComboBox.ItemsSource = _roomList;
            RoomBComboBox.ItemsSource = _roomList;
            PresentationEffectKeyComboBox.ItemsSource = configuredEffects;

            _suppressSelectionHandlers = true;
            RoomAComboBox.SelectedValue = connection.RoomAId;
            RoomBComboBox.SelectedValue = connection.RoomBId;
            TraversalAccessModeComboBox.SelectedItem = connection.TraversalAccessMode;
            OpenStateBindingModeComboBox.SelectedItem = connection.OpenStateBindingMode;
            OpenStatePolicyFromAComboBox.SelectedItem = stateFromA.OpenStatePolicy;
            OpenStatePolicyFromBComboBox.SelectedItem = stateFromB.OpenStatePolicy;
            PresentationEffectKeyComboBox.SelectedValue = NormalizeOptionalText(connection.PresentationEffectKey);
            _suppressSelectionHandlers = false;

            var supportsGuided = _guidedDestinationsByRoom.Count > 0;
            GuidedModeRadioButton.IsEnabled = supportsGuided;
            UnguidedModeRadioButton.IsEnabled = supportsGuided;

            if (supportsGuided && IsRepresentableInGuidedMode(connection))
            {
                GuidedModeRadioButton.IsChecked = true;
            }
            else
            {
                UnguidedModeRadioButton.IsChecked = true;
            }

            ApplyModeBehavior(autoSelectToRoom: false, preferredDirection: connection.BaseTraversalDirectionFromA);

            RefreshDoorOptions(stateFromA.OpenableObjectId, stateFromB.OpenableObjectId);
        }
        finally
        {
            _isInitializingDialog = false;
        }
    }

    public Guid RoomAId => RoomAComboBox.SelectedValue is Guid id ? id : Guid.Empty;
    public Guid RoomBId => RoomBComboBox.SelectedValue is Guid id ? id : Guid.Empty;

    public Direction10 BaseTraversalDirectionFromA => DirectionComboBox.SelectedItem is Direction10 direction
        ? direction
        : Direction10.North;

    public TraversalAccessMode TraversalAccessMode => TraversalAccessModeComboBox.SelectedItem is TraversalAccessMode accessMode
        ? accessMode
        : TraversalAccessMode.TwoWay;

    public OpenStateBindingMode OpenStateBindingMode => OpenStateBindingModeComboBox.SelectedItem is OpenStateBindingMode mode
        ? mode
        : OpenStateBindingMode.Independent;

    public OpenablePolicy OpenStatePolicyFromA => OpenStatePolicyFromAComboBox.SelectedItem is OpenablePolicy policy
        ? policy
        : OpenablePolicy.IgnoreOpenableState;

    public OpenablePolicy OpenStatePolicyFromB => OpenStatePolicyFromBComboBox.SelectedItem is OpenablePolicy policy
        ? policy
        : OpenablePolicy.IgnoreOpenableState;

    public Guid? FromRoomDoorObjectId => FromRoomDoorComboBox.SelectedValue is Guid id ? id : null;

    public Guid? ToRoomDoorObjectId => ToRoomDoorComboBox.SelectedValue is Guid id ? id : null;

    public string? PresentationEffectKey => NormalizeOptionalText(PresentationEffectKeyComboBox.SelectedValue as string);

    private IReadOnlyList<PresentationEffectOption> BuildRoomTransitionEffectOptions(string? selectedEffectKey)
    {
        var options = _presentationEffectsCatalogService
            .GetConfiguredRoomTransitionEffectKeys()
            .Select(static key => new PresentationEffectOption
            {
                EffectKey = key,
                Label = key
            })
            .ToList();

        options.Insert(0, new PresentationEffectOption
        {
            EffectKey = null,
            Label = "(Default)"
        });

        var normalizedSelected = NormalizeOptionalText(selectedEffectKey);
        if (!string.IsNullOrWhiteSpace(normalizedSelected)
            && options.All(option => !string.Equals(option.EffectKey, normalizedSelected, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new PresentationEffectOption
            {
                EffectKey = normalizedSelected,
                Label = normalizedSelected
            });
        }

        return options;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private bool IsGuidedMode => GuidedModeRadioButton.IsChecked == true;

    private void ModeRadioButton_OnChecked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_suppressSelectionHandlers
            || _isInitializingDialog
            || DirectionComboBox is null
            || RoomAComboBox is null
            || RoomBComboBox is null)
        {
            return;
        }

        ApplyModeBehavior(autoSelectToRoom: IsGuidedMode, preferredDirection: DirectionComboBox.SelectedItem as Direction10?);
    }

    private void RoomComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_suppressSelectionHandlers)
        {
            return;
        }

        ApplyModeBehavior(autoSelectToRoom: IsGuidedMode, preferredDirection: DirectionComboBox.SelectedItem as Direction10?);
        RefreshDoorOptions(FromRoomDoorObjectId, ToRoomDoorObjectId);
    }

    private void DirectionComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_suppressSelectionHandlers || !IsGuidedMode)
        {
            return;
        }

        AutoSelectDestinationFromDirection();
        RefreshDoorOptions(FromRoomDoorObjectId, ToRoomDoorObjectId);
    }

    private void RefreshDoorOptions(Guid? selectedFromDoorId, Guid? selectedToDoorId)
    {
        var fromRoomId = RoomAComboBox.SelectedValue as Guid?;
        var toRoomId = RoomBComboBox.SelectedValue as Guid?;

        var fromOptions = BuildDoorOptions(fromRoomId);
        var toOptions = BuildDoorOptions(toRoomId);

        FromRoomDoorComboBox.ItemsSource = fromOptions;
        ToRoomDoorComboBox.ItemsSource = toOptions;

        FromRoomDoorComboBox.SelectedValue = fromOptions.Any(option => option.ObjectId == selectedFromDoorId)
            ? selectedFromDoorId
            : null;
        ToRoomDoorComboBox.SelectedValue = toOptions.Any(option => option.ObjectId == selectedToDoorId)
            ? selectedToDoorId
            : null;
    }

    private List<DoorOption> BuildDoorOptions(Guid? roomId)
    {
        var options = new List<DoorOption>
        {
            new DoorOption { ObjectId = null, Label = "(None)" }
        };

        if (!roomId.HasValue || !_roomsById.TryGetValue(roomId.Value, out var room))
        {
            return options;
        }

        foreach (var obj in EnumerateObjects(room.GameObjects ?? new List<GameObject>())
                     .Where(candidate => candidate.IsOpenable && !candidate.IsInventoriable)
                     .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new DoorOption
            {
                ObjectId = obj.ObjectId,
                Label = obj.Name
            });
        }

        return options;
    }

    private static IEnumerable<GameObject> EnumerateObjects(IEnumerable<GameObject> roots)
    {
        if (roots is null)
        {
            yield break;
        }

        var stack = new Stack<GameObject>();
        var visited = new HashSet<Guid>();
        foreach (var root in roots)
        {
            if (root is not null)
            {
                stack.Push(root);
            }
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is null)
            {
                continue;
            }

            if (current.ObjectId != Guid.Empty && !visited.Add(current.ObjectId))
            {
                continue;
            }

            yield return current;

            var children = current.ContainedObjects;
            if (children is null)
            {
                continue;
            }

            for (var index = children.Count - 1; index >= 0; index--)
            {
                var child = children[index];
                if (child is not null)
                {
                    stack.Push(child);
                }
            }
        }
    }

    private void ApplyModeBehavior(bool autoSelectToRoom, Direction10? preferredDirection)
    {
        _suppressSelectionHandlers = true;
        try
        {
            if (!IsGuidedMode || !GuidedModeRadioButton.IsEnabled)
            {
                DirectionComboBox.ItemsSource = _allowedDirections;
                var selectedDirection = preferredDirection.HasValue && _allowedDirections.Contains(preferredDirection.Value)
                    ? preferredDirection.Value
                    : _allowedDirections.FirstOrDefault();
                DirectionComboBox.SelectedItem = selectedDirection;
                RoomBComboBox.ItemsSource = _roomList;
                return;
            }

            var fromRoomId = RoomAComboBox.SelectedValue is Guid fromId ? fromId : Guid.Empty;
            var destinations = _guidedDestinationsByRoom.TryGetValue(fromRoomId, out var mapped)
                ? mapped
                : new Dictionary<Direction10, Guid>();

            var availableDirections = destinations.Keys
                .Where(_allowedDirections.Contains)
                .OrderBy(DirectionOrder)
                .ToList();
            DirectionComboBox.ItemsSource = availableDirections;

            var desiredDirection = preferredDirection.HasValue && availableDirections.Contains(preferredDirection.Value)
                ? preferredDirection.Value
                : availableDirections.FirstOrDefault();

            DirectionComboBox.SelectedItem = availableDirections.Count > 0 ? desiredDirection : null;

            RoomBComboBox.ItemsSource = _roomList;

            if (autoSelectToRoom)
            {
                AutoSelectDestinationFromDirection();
            }
        }
        finally
        {
            _suppressSelectionHandlers = false;
        }
    }

    private void AutoSelectDestinationFromDirection()
    {
        if (RoomAComboBox.SelectedValue is not Guid fromRoomId
            || DirectionComboBox.SelectedItem is not Direction10 selectedDirection
            || !_guidedDestinationsByRoom.TryGetValue(fromRoomId, out var byDirection)
            || !byDirection.TryGetValue(selectedDirection, out var toRoomId))
        {
            return;
        }

        RoomBComboBox.SelectedValue = toRoomId;
    }

    private bool IsRepresentableInGuidedMode(TraversalConnection connection)
    {
        return _guidedDestinationsByRoom.TryGetValue(connection.RoomAId, out var byDirection)
               && byDirection.TryGetValue(connection.BaseTraversalDirectionFromA, out var destinationId)
               && destinationId == connection.RoomBId;
    }

    private void BuildGuidedDestinations(Area? area)
    {
        _guidedDestinationsByRoom.Clear();
        if (area is null)
        {
            return;
        }

        var areaRooms = (area.Rooms ?? new List<Room>())
            .Where(room => room is not null)
            .ToList();
        var areaPlacements = (area.RoomPlacements ?? new List<AreaRoomPlacement>())
            .Where(placement => placement is not null)
            .ToList();
        if (areaPlacements.Count == 0)
        {
            return;
        }

        var roomById = areaRooms
            .Where(room => room.Id != Guid.Empty)
            .GroupBy(room => room.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var placementByRoomId = areaPlacements
            .Where(placement => placement.RoomId != Guid.Empty)
            .GroupBy(placement => placement.RoomId)
            .ToDictionary(group => group.Key, group => group.First());

        var roomIdByCell = new Dictionary<(int col, int row), Guid>();
        foreach (var (roomId, placement) in placementByRoomId)
        {
            roomIdByCell[ToGridCell(placement.X, placement.Y)] = roomId;
        }

        foreach (var (roomId, placement) in placementByRoomId)
        {
            if (!roomById.ContainsKey(roomId))
            {
                continue;
            }

            var sourceCell = ToGridCell(placement.X, placement.Y);
            var byDirection = new Dictionary<Direction10, Guid>();

            foreach (var (dx, dy, direction) in DirectionDeltas())
            {
                if (area.AdjacencyMode == AreaAdjacencyMode.FourDirectional && dx != 0 && dy != 0)
                {
                    continue;
                }

                var targetCell = (sourceCell.col + dx, sourceCell.row + dy);
                if (!roomIdByCell.TryGetValue(targetCell, out var targetRoomId) || targetRoomId == roomId)
                {
                    continue;
                }

                byDirection[direction] = targetRoomId;
            }

            _guidedDestinationsByRoom[roomId] = byDirection;
        }
    }

    private static (int col, int row) ToGridCell(double x, double y)
    {
        const double cellWidth = 180;
        const double cellHeight = 140;
        const double gridOriginX = 24;
        const double gridOriginY = 24;

        var col = (int)Math.Round((x - gridOriginX) / cellWidth, MidpointRounding.AwayFromZero);
        var row = (int)Math.Round((y - gridOriginY) / cellHeight, MidpointRounding.AwayFromZero);
        return (Math.Max(0, col), Math.Max(0, row));
    }

    private static IEnumerable<(int dx, int dy, Direction10 direction)> DirectionDeltas()
    {
        yield return (0, -1, Direction10.North);
        yield return (1, -1, Direction10.NorthEast);
        yield return (1, 0, Direction10.East);
        yield return (1, 1, Direction10.SouthEast);
        yield return (0, 1, Direction10.South);
        yield return (-1, 1, Direction10.SouthWest);
        yield return (-1, 0, Direction10.West);
        yield return (-1, -1, Direction10.NorthWest);
    }

    private static int DirectionOrder(Direction10 direction)
    {
        return direction switch
        {
            Direction10.North => 0,
            Direction10.NorthEast => 1,
            Direction10.East => 2,
            Direction10.SouthEast => 3,
            Direction10.South => 4,
            Direction10.SouthWest => 5,
            Direction10.West => 6,
            Direction10.NorthWest => 7,
            Direction10.Up => 8,
            Direction10.Down => 9,
            _ => int.MaxValue
        };
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (RoomAId == Guid.Empty || RoomBId == Guid.Empty)
        {
            System.Windows.MessageBox.Show(this,
                "Select both rooms for this traversal.",
                "Edit Traversal",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (RoomAId == RoomBId)
        {
            System.Windows.MessageBox.Show(this,
                "A traversal cannot point to the same room on both sides.",
                "Edit Traversal",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
