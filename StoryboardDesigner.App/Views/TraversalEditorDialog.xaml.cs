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
    private IReadOnlyList<TraversalConnection> _areaConnections = Array.Empty<TraversalConnection>();
    private Guid _editingConnectionId;
    private Guid? _originalFromDoorId;
    private Guid? _originalToDoorId;
    private readonly Dictionary<Guid, Dictionary<Guid, Direction10>> _guidedDirectionsByPair = new();
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
            _editingConnectionId = connection.TraversalConnectionId;
            _originalFromDoorId = stateFromA.OpenableObjectId;
            _originalToDoorId = stateFromB.OpenableObjectId;
            _areaConnections = area?.TraversalConnections ?? new List<TraversalConnection>();
            IReadOnlyCollection<Room> safeRooms = rooms ?? area?.Rooms ?? new List<Room>();

            _roomList = safeRooms
                .Where(room => room is not null)
                .GroupBy(room => room.Id)
                .Select(group => group.First())
                .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _roomsById = _roomList.ToDictionary(room => room.Id);
            BuildGuidedPairs(area);
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

            var supportsGuided = _guidedDirectionsByPair.Count > 0;
            GuidedModeRadioButton.IsEnabled = supportsGuided;
            UnguidedModeRadioButton.IsEnabled = supportsGuided;

            var isNewConnection = _areaConnections.All(existing => existing.TraversalConnectionId != connection.TraversalConnectionId);
            var canGuideNewConnection = isNewConnection
                && _guidedDirectionsByPair.TryGetValue(connection.RoomAId, out var candidates)
                && candidates.Count > 0
                && (connection.RoomBId == Guid.Empty || candidates.ContainsKey(connection.RoomBId));
            if (supportsGuided && (IsRepresentableInGuidedMode(connection) || canGuideNewConnection))
            {
                GuidedModeRadioButton.IsChecked = true;
            }
            else
            {
                UnguidedModeRadioButton.IsChecked = true;
            }

            ApplyModeBehavior(autoSelectToRoom: isNewConnection, preferredDirection: connection.BaseTraversalDirectionFromA);

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

        if (IsGuidedMode && ReferenceEquals(sender, RoomBComboBox))
        {
            UpdateGuidedDirectionFromSelectedPair();
        }
        else
        {
            ApplyModeBehavior(autoSelectToRoom: IsGuidedMode, preferredDirection: DirectionComboBox.SelectedItem as Direction10?);
        }
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

        FromRoomDoorComboBox.SelectedItem = fromOptions.FirstOrDefault(option =>
            option.ObjectId.HasValue && option.ObjectId == selectedFromDoorId) ?? fromOptions[0];
        ToRoomDoorComboBox.SelectedItem = toOptions.FirstOrDefault(option =>
            option.ObjectId.HasValue && option.ObjectId == selectedToDoorId) ?? toOptions[0];
    }

    private List<DoorOption> BuildDoorOptions(Guid? roomId)
    {
        var options = new List<DoorOption>
        {
            new DoorOption { ObjectId = null, Label = "No door (open passage)" }
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
                Label = $"{obj.Name} ({obj.PositionX:0.##}, {obj.PositionY:0.##}) · {obj.ObjectId:N}"
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
            var selectedToRoomId = RoomBComboBox.SelectedValue as Guid?;
            if (!IsGuidedMode || !GuidedModeRadioButton.IsEnabled)
            {
                RoomBComboBox.ItemsSource = _roomList;
                RoomBComboBox.SelectedValue = selectedToRoomId;
                DirectionComboBox.ItemsSource = _allowedDirections;
                var selectedDirection = preferredDirection.HasValue && _allowedDirections.Contains(preferredDirection.Value)
                    ? preferredDirection.Value
                    : _allowedDirections.FirstOrDefault();
                DirectionComboBox.SelectedItem = selectedDirection;
                DirectionComboBox.IsEnabled = true;
                return;
            }

            var fromRoomId = RoomAComboBox.SelectedValue is Guid fromId ? fromId : Guid.Empty;
            var destinations = _guidedDirectionsByPair.TryGetValue(fromRoomId, out var mapped)
                ? mapped
                : new Dictionary<Guid, Direction10>();
            var availableRooms = destinations
                .Where(pair => _allowedDirections.Contains(pair.Value) && _roomsById.ContainsKey(pair.Key))
                .Select(pair => _roomsById[pair.Key])
                .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            RoomBComboBox.ItemsSource = availableRooms;
            RoomBComboBox.SelectedValue = selectedToRoomId.HasValue && availableRooms.Any(room => room.Id == selectedToRoomId.Value)
                ? selectedToRoomId
                : autoSelectToRoom && availableRooms.Count == 1 ? availableRooms[0].Id : null;
            DirectionComboBox.ItemsSource = _allowedDirections;
            DirectionComboBox.IsEnabled = false;
            UpdateGuidedDirectionFromSelectedPair();
        }
        finally
        {
            _suppressSelectionHandlers = false;
        }
    }

    private void UpdateGuidedDirectionFromSelectedPair()
    {
        if (RoomAComboBox.SelectedValue is not Guid fromRoomId
            || RoomBComboBox.SelectedValue is not Guid toRoomId
            || !_guidedDirectionsByPair.TryGetValue(fromRoomId, out var byDestination)
            || !byDestination.TryGetValue(toRoomId, out var direction))
        {
            DirectionComboBox.SelectedItem = null;
            return;
        }

        DirectionComboBox.SelectedItem = direction;
    }

    private bool IsRepresentableInGuidedMode(TraversalConnection connection)
    {
        return _guidedDirectionsByPair.TryGetValue(connection.RoomAId, out var byDestination)
               && byDestination.TryGetValue(connection.RoomBId, out var direction)
               && direction == connection.BaseTraversalDirectionFromA
               && _allowedDirections.Contains(direction);
    }

    private void BuildGuidedPairs(Area? area)
    {
        _guidedDirectionsByPair.Clear();
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

        foreach (var (roomId, placement) in placementByRoomId)
        {
            if (!roomById.ContainsKey(roomId))
            {
                continue;
            }

            var byDestination = new Dictionary<Guid, Direction10>();
            foreach (var (targetRoomId, targetPlacement) in placementByRoomId)
            {
                if (targetRoomId == roomId || !roomById.ContainsKey(targetRoomId))
                {
                    continue;
                }
                if (!AreaMapGeometry.FromPlacement(placement).TryGetAdjacentOffset(
                        AreaMapGeometry.FromPlacement(targetPlacement),
                        area.AdjacencyMode != AreaAdjacencyMode.FourDirectional, out var dx, out var dy))
                {
                    continue;
                }
                byDestination[targetRoomId] = DirectionFromOffset(dx, dy);
            }

            _guidedDirectionsByPair[roomId] = byDestination;
        }
    }

    private static Direction10 DirectionFromOffset(int dx, int dy)
    {
        return (dx, dy) switch
        {
            (0, -1) => Direction10.North,
            (1, -1) => Direction10.NorthEast,
            (1, 0) => Direction10.East,
            (1, 1) => Direction10.SouthEast,
            (0, 1) => Direction10.South,
            (-1, 1) => Direction10.SouthWest,
            (-1, 0) => Direction10.West,
            (-1, -1) => Direction10.NorthWest,
            _ => throw new ArgumentOutOfRangeException(nameof(dx))
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

        if (DirectionComboBox.SelectedItem is not Direction10)
        {
            System.Windows.MessageBox.Show(this,
                "Select an adjacent destination or switch to Unguided to choose a direction.",
                "Edit Traversal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectedDoorIds = new[] { FromRoomDoorObjectId, ToRoomDoorObjectId }
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Where(id => id != _originalFromDoorId && id != _originalToDoorId)
            .ToHashSet();
        if (_areaConnections.Any(connection => connection.TraversalConnectionId != _editingConnectionId
            && (connection.TraversalStateFromA.OpenableObjectId is Guid fromDoorId && selectedDoorIds.Contains(fromDoorId)
                || connection.TraversalStateFromB.OpenableObjectId is Guid toDoorId && selectedDoorIds.Contains(toDoorId))))
        {
            System.Windows.MessageBox.Show(this,
                "A selected door is already linked to another traversal. Choose a separate door for each connection.",
                "Edit Traversal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
