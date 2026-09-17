using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for AreaRoomPlacementCard.xaml
/// </summary>
public partial class AreaRoomPlacementCard : System.Windows.Controls.UserControl
{
    private AreaRoomPlacementViewModel? _dragPlacement;
    private System.Windows.Point _dragStart;
    private System.Windows.Point _dragPlacementStart;
    private bool _isPlacementDragActive;

    private enum VerticalSeedPreference
    {
        None,
        Up,
        Down
    }

    public AreaRoomPlacementCard()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? GetViewModel() => Window.GetWindow(this)?.DataContext as MainWindowViewModel;

    private void NavigationPlacement_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel is null || sender is not Border border || border.DataContext is not AreaRoomPlacementViewModel placement)
        {
            return;
        }

        viewModel.SetSelectedTraversalSourceRoom(placement.Room.Id);

        var mapCanvas = FindAncestor<AreaMapCanvas>(border);
        if (mapCanvas?.TryApplyPendingDestinationForPlacement(placement.Room, viewModel) == true)
        {
            e.Handled = true;
            return;
        }

        _dragPlacement = placement;
        _dragStart = e.GetPosition(border);
        _dragPlacementStart = new System.Windows.Point(placement.X, placement.Y);
        _isPlacementDragActive = true;
        if (FindAncestor<Canvas>(border)?.DataContext is AreaNavigationEditorTabViewModel editor)
        {
            editor.BeginPlacementDrag(placement);
        }

        border.CaptureMouse();
        e.Handled = true;
    }

    private void NavigationPlacement_OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPlacementDragActive || sender is not Border border || _dragPlacement is null)
        {
            return;
        }

        var canvas = FindAncestor<Canvas>(border);
        if (canvas is null || canvas.DataContext is not AreaNavigationEditorTabViewModel editor)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        editor.TryPreviewPlacement(_dragPlacement, point.X - _dragStart.X, point.Y - _dragStart.Y);
    }

    private void NavigationPlacement_OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel is null || sender is not Border border || _dragPlacement is null)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        if (FindAncestor<Canvas>(border)?.DataContext is AreaNavigationEditorTabViewModel editor)
        {
            var moved = Math.Abs(_dragPlacement.X - _dragPlacementStart.X) >= 0.01
                || Math.Abs(_dragPlacement.Y - _dragPlacementStart.Y) >= 0.01;

            if (moved)
            {
                var connectedTraversalCount = editor.GetConnectedTraversalCount(_dragPlacement.Room.Id);
                if (connectedTraversalCount > 0)
                {
                    var deletedConnectionIds = editor.TraversalConnections
                        .Where(connection => connection.RoomAId == _dragPlacement.Room.Id || connection.RoomBId == _dragPlacement.Room.Id)
                        .Select(connection => connection.TraversalConnectionId)
                        .Distinct()
                        .ToList();

                    var choice = System.Windows.MessageBox.Show(
                        owner,
                        $"Moving this room will delete {connectedTraversalCount} traversal connection(s) tied to it.\n\nSelect Yes to move and delete traversals, or No to put it back.",
                        "Room Move Removes Traversals",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (choice != MessageBoxResult.Yes)
                    {
                        editor.CancelPlacementDrag(_dragPlacement);
                    }
                    else if (editor.CommitPlacementDrag(_dragPlacement, deleteConnectedTraversals: true, out var deletedCount))
                    {
                        var removedSharedParticipants = viewModel.RemoveTraversalSharedParticipantsForDeletedTraversals(deletedConnectionIds);
                        System.Windows.MessageBox.Show(
                            owner,
                            $"Room moved. Removed {deletedCount} traversal connection(s). Detached {removedSharedParticipants} shared link participant(s).",
                            "Room Move Applied",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        viewModel.NotifyProjectEdited();
                    }
                }
                else if (editor.CommitPlacementDrag(_dragPlacement, deleteConnectedTraversals: false, out _))
                {
                    viewModel.NotifyProjectEdited();
                }
            }
            else
            {
                editor.CancelPlacementDrag(_dragPlacement);
            }
        }

        _isPlacementDragActive = false;
        _dragPlacement = null;
        border.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void NavigationPlacementOpenWizard_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel is null || sender is not MenuItem menuItem || menuItem.DataContext is not AreaRoomPlacementViewModel placement)
        {
            return;
        }

        viewModel.SetSelectedTraversalSourceRoom(placement.Room.Id);
        viewModel.TryRunTraversalWizardForRoom(placement.Room);
    }

    private void NavigationPlacementReviewTraversals_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel is null || sender is not MenuItem menuItem || menuItem.DataContext is not AreaRoomPlacementViewModel placement)
        {
            return;
        }

        ShowRoomTraversalReviewDialog(viewModel, placement.Room, VerticalSeedPreference.None, openAddDialogImmediately: false);
    }

    private void VerticalIndicator_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel is null || sender is not System.Windows.Controls.Button button || button.DataContext is not AreaRoomPlacementViewModel placement)
        {
            return;
        }

        viewModel.SetSelectedTraversalSourceRoom(placement.Room.Id);

        var indicatorCode = (button.Content as string)?.Trim() ?? string.Empty;
        var preference = ResolveVerticalSeedPreferenceFromIndicatorCode(indicatorCode);

        ShowRoomTraversalReviewDialog(viewModel, placement.Room, preference, openAddDialogImmediately: true);
    }

    private static VerticalSeedPreference ResolveVerticalSeedPreferenceFromIndicatorCode(string? indicatorCode)
    {
        var normalized = indicatorCode?.Trim() ?? string.Empty;
        return normalized is "RU" or "TU"
            ? VerticalSeedPreference.Up
            : normalized is "RD" or "TD"
                ? VerticalSeedPreference.Down
                : VerticalSeedPreference.None;
    }

    private static IReadOnlyCollection<Direction10> ResolveAllowedTraversalDirections(VerticalSeedPreference verticalSeedPreference)
    {
        return verticalSeedPreference == VerticalSeedPreference.None
            ? DirectionValues.DefaultTraversalDirections
            : DirectionValues.VerticalTraversalDirections;
    }

    private static IReadOnlyCollection<Direction10> ResolveAllowedTraversalDirectionsForEdit(TraversalConnection connection)
    {
        if (connection.BaseTraversalDirectionFromA is Direction10.Up or Direction10.Down)
        {
            return DirectionValues.All10;
        }

        return DirectionValues.DefaultTraversalDirections;
    }

    private void ShowRoomTraversalReviewDialog(
        MainWindowViewModel viewModel,
        Room room,
        VerticalSeedPreference verticalSeedPreference,
        bool openAddDialogImmediately)
    {
        var owner = Window.GetWindow(this);
        var pendingSeedPreference = verticalSeedPreference;

        if (openAddDialogImmediately)
        {
            if (TryRunAddTraversalDialog(viewModel, room, pendingSeedPreference))
            {
                viewModel.NotifyProjectEdited();
            }

            return;
        }

        while (viewModel.SelectedAreaEditor is not null)
        {
            var editor = viewModel.SelectedAreaEditor;
            var area = editor.Area;
            var reviewRows = BuildRoomTraversalReviewRows(area, room);

            var dialog = new RoomTraversalsDialog(room.Name, reviewRows)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() != true || dialog.SelectedConnection is null)
            {
                if (dialog.Action != RoomTraversalsDialogAction.Add)
                {
                    return;
                }
            }

            if (dialog.Action == RoomTraversalsDialogAction.Add)
            {
                if (!TryRunAddTraversalDialog(viewModel, room, pendingSeedPreference))
                {
                    continue;
                }

                viewModel.NotifyProjectEdited();
                pendingSeedPreference = VerticalSeedPreference.None;
                continue;
            }

            if (dialog.SelectedConnection is null)
            {
                return;
            }

            if (dialog.Action == RoomTraversalsDialogAction.Relink)
            {
                var relinked = viewModel.TryRelinkTraversalDoors(dialog.SelectedConnection, out var relinkWarningMessage);
                if (!relinked)
                {
                    System.Windows.MessageBox.Show(
                        owner,
                        "Could not relink traversal doors. Ensure the linked door objects still exist in both rooms.",
                        "Manage Traversals",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(relinkWarningMessage))
                {
                    System.Windows.MessageBox.Show(
                        owner,
                        relinkWarningMessage,
                        "Traversal Door Link Review",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                viewModel.NotifyProjectEdited();
                continue;
            }

            if (dialog.Action == RoomTraversalsDialogAction.Remove)
            {
                var choice = System.Windows.MessageBox.Show(
                    owner,
                    "Remove selected traversal from this area?",
                    "Manage Traversals",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (choice != MessageBoxResult.Yes)
                {
                    continue;
                }

                viewModel.RemoveNavigationLink(dialog.SelectedConnection);
                viewModel.NotifyProjectEdited();
                continue;
            }

            if (dialog.Action != RoomTraversalsDialogAction.Edit)
            {
                return;
            }

            TraversalEditorDialog editDialog;
            try
            {
                editDialog = new TraversalEditorDialog(
                    dialog.SelectedConnection,
                    area.Rooms,
                    area,
                    ResolveAllowedTraversalDirectionsForEdit(dialog.SelectedConnection))
                {
                    Owner = owner
                };
            }
            catch (Exception ex)
            {
                ShowTraversalDialogCrash("open traversal editor (edit)", ex);
                return;
            }

            bool? editAccepted;
            try
            {
                editAccepted = editDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowTraversalDialogCrash("display traversal editor (edit)", ex);
                return;
            }

            if (editAccepted != true)
            {
                continue;
            }

            var updated = viewModel.TryUpdateSelectedAreaTraversal(
                dialog.SelectedConnection,
                editDialog.RoomAId,
                editDialog.RoomBId,
                editDialog.BaseTraversalDirectionFromA,
                editDialog.TraversalAccessMode,
                editDialog.OpenStateBindingMode,
                editDialog.OpenStatePolicyFromA,
                editDialog.OpenStatePolicyFromB,
                editDialog.PresentationEffectKey);

            if (!updated)
            {
                System.Windows.MessageBox.Show(
                    owner,
                    "Could not update this traversal. Verify both rooms still exist and are distinct.",
                    "Manage Traversals",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                continue;
            }

            if (viewModel.TryLinkTraversalDoors(
                    dialog.SelectedConnection,
                    editDialog.FromRoomDoorObjectId,
                    editDialog.ToRoomDoorObjectId,
                    out var warningMessage)
                && !string.IsNullOrWhiteSpace(warningMessage))
            {
                System.Windows.MessageBox.Show(
                    owner,
                    warningMessage,
                    "Traversal Door Link Review",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            viewModel.NotifyProjectEdited();
        }
    }

    private bool TryRunAddTraversalDialog(MainWindowViewModel viewModel, Room sourceRoom, VerticalSeedPreference verticalSeedPreference)
    {
        if (viewModel.SelectedAreaEditor is null)
        {
            return false;
        }

        var owner = Window.GetWindow(this);
        var area = viewModel.SelectedAreaEditor.Area;
        var seed = BuildAddTraversalSeed(area, sourceRoom, verticalSeedPreference);
        var allowedDirections = ResolveAllowedTraversalDirections(verticalSeedPreference);

        TraversalEditorDialog addDialog;
        try
        {
            addDialog = new TraversalEditorDialog(seed, area.Rooms, area, allowedDirections)
            {
                Owner = owner
            };
        }
        catch (Exception ex)
        {
            ShowTraversalDialogCrash("open traversal editor (add)", ex);
            return false;
        }

        bool? addAccepted;
        try
        {
            addAccepted = addDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowTraversalDialogCrash("display traversal editor (add)", ex);
            return false;
        }

        if (addAccepted != true)
        {
            return false;
        }

        var created = viewModel.TryCreateSelectedAreaTraversal(
            addDialog.RoomAId,
            addDialog.RoomBId,
            addDialog.BaseTraversalDirectionFromA,
            addDialog.TraversalAccessMode,
            addDialog.OpenStateBindingMode,
            addDialog.OpenStatePolicyFromA,
            addDialog.OpenStatePolicyFromB,
            addDialog.PresentationEffectKey,
            out var createdConnection);

        if (!created)
        {
            System.Windows.MessageBox.Show(
                owner,
                "Could not create traversal. Verify both rooms are valid and the connection does not already exist.",
                "Manage Traversals",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (createdConnection is not null
            && viewModel.TryLinkTraversalDoors(
                createdConnection,
                addDialog.FromRoomDoorObjectId,
                addDialog.ToRoomDoorObjectId,
                out var addWarningMessage)
            && !string.IsNullOrWhiteSpace(addWarningMessage))
        {
            System.Windows.MessageBox.Show(
                owner,
                addWarningMessage,
                "Traversal Door Link Review",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        return true;
    }

    private static List<RoomTraversalReviewItem> BuildRoomTraversalReviewRows(Area area, Room sourceRoom)
    {
        var areaRooms = area.Rooms ?? new List<Room>();

        var roomNameById = areaRooms
            .GroupBy(room => room.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var roomById = areaRooms
            .GroupBy(room => room.Id)
            .ToDictionary(group => group.Key, group => group.First());

        return area.TraversalConnections
            .Where(connection => connection.RoomAId == sourceRoom.Id || connection.RoomBId == sourceRoom.Id)
            .Select(connection =>
            {
                var fromRoomA = connection.RoomAId == sourceRoom.Id;
                var destinationRoomId = fromRoomA ? connection.RoomBId : connection.RoomAId;
                var direction = fromRoomA
                    ? connection.BaseTraversalDirectionFromA
                    : TraversalWizardApplyUtility.OppositeDirection(connection.BaseTraversalDirectionFromA);

                var fromDoorId = fromRoomA
                    ? connection.TraversalStateFromA.OpenableObjectId
                    : connection.TraversalStateFromB.OpenableObjectId;
                var toDoorId = fromRoomA
                    ? connection.TraversalStateFromB.OpenableObjectId
                    : connection.TraversalStateFromA.OpenableObjectId;

                var destinationRoom = roomById.TryGetValue(destinationRoomId, out var dest) ? dest : null;
                var fromDoorName = TryResolveRoomObjectName(sourceRoom, fromDoorId);
                var toDoorName = destinationRoom is null ? null : TryResolveRoomObjectName(destinationRoom, toDoorId);

                var linkSummary = BuildDoorLinkSummary(fromDoorName, toDoorName);
                var fromLeg = fromRoomA ? connection.TraversalStateFromA : connection.TraversalStateFromB;
                var toLeg = fromRoomA ? connection.TraversalStateFromB : connection.TraversalStateFromA;

                return new RoomTraversalReviewItem
                {
                    Connection = connection,
                    DirectionLabel = direction.ToString(),
                    DestinationRoomName = roomNameById.TryGetValue(destinationRoomId, out var name) ? name : "Unknown Room",
                    DoorLinkSummary = linkSummary,
                    FromPassableSharedSummary = IsPassableBoundToSharedVariable(fromLeg) ? "Shared" : "Not Shared",
                    ToPassableSharedSummary = IsPassableBoundToSharedVariable(toLeg) ? "Shared" : "Not Shared",
                    PresentationEffectSummary = string.IsNullOrWhiteSpace(connection.PresentationEffectKey)
                        ? "(Default)"
                        : connection.PresentationEffectKey
                };
            })
            .OrderBy(item => item.DirectionLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DestinationRoomName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsPassableBoundToSharedVariable(TraversalLegState legState)
    {
        if (legState is null)
        {
            return false;
        }

        var passable = legState.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));

        if (passable?.SharedVariableId is Guid passableSharedId && passableSharedId != Guid.Empty)
        {
            return true;
        }

        return legState.SharedVariableId is Guid sharedId && sharedId != Guid.Empty;
    }

    private static string BuildDoorLinkSummary(string? fromDoorName, string? toDoorName)
    {
        var hasFrom = !string.IsNullOrWhiteSpace(fromDoorName);
        var hasTo = !string.IsNullOrWhiteSpace(toDoorName);
        if (!hasFrom && !hasTo)
        {
            return "None";
        }

        if (hasFrom && hasTo)
        {
            return $"Both ({fromDoorName} <-> {toDoorName})";
        }

        return hasFrom ? $"From only ({fromDoorName})" : $"To only ({toDoorName})";
    }

    private static string? TryResolveRoomObjectName(Room room, Guid? objectId)
    {
        if (!objectId.HasValue || objectId.Value == Guid.Empty)
        {
            return null;
        }

        return EnumerateRoomObjects(room.GameObjects)
            .Where(obj => obj.ObjectId == objectId.Value)
            .Select(obj => obj.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
    }

    private static IEnumerable<GameObject> EnumerateRoomObjects(IEnumerable<GameObject> roots)
    {
        if (roots is null)
        {
            yield break;
        }

        foreach (var root in roots)
        {
            if (root is null)
            {
                continue;
            }

            yield return root;
            foreach (var child in EnumerateRoomObjects(root.ContainedObjects))
            {
                yield return child;
            }
        }
    }

    private void ShowTraversalDialogCrash(string stage, Exception ex)
    {
        var firstFrame = ex.StackTrace?
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        var locationText = string.IsNullOrWhiteSpace(firstFrame) ? string.Empty : $"\n{firstFrame}";
        var message = $"Traversal dialog crashed while trying to {stage}.\n\n{ex.GetType().Name}: {ex.Message}{locationText}";
        System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            message,
            "Traversal Dialog Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static TraversalConnection BuildAddTraversalSeed(Area area, Room sourceRoom, VerticalSeedPreference verticalSeedPreference)
    {
        var placementByRoomId = (area.RoomPlacements ?? new List<AreaRoomPlacement>())
            .Where(placement => placement.RoomId != Guid.Empty)
            .GroupBy(placement => placement.RoomId)
            .ToDictionary(group => group.Key, group => group.First());

        var sourcePlacement = placementByRoomId.TryGetValue(sourceRoom.Id, out var source)
            ? source
            : null;

        var destinationRoom = ResolveSeedDestinationRoom(area, sourceRoom, sourcePlacement, placementByRoomId, verticalSeedPreference)
            ?? (area.Rooms ?? new List<Room>()).FirstOrDefault(candidate => candidate.Id != sourceRoom.Id);

        var preferredFromSource = verticalSeedPreference switch
        {
            VerticalSeedPreference.Up => Direction10.Up,
            VerticalSeedPreference.Down => Direction10.Down,
            _ => Direction10.East
        };

        return new TraversalConnection
        {
            RoomAId = sourceRoom.Id,
            RoomBId = destinationRoom?.Id ?? Guid.Empty,
            BaseTraversalDirectionFromA = preferredFromSource,
            TraversalAccessMode = TraversalAccessMode.TwoWay,
            OpenStateBindingMode = OpenStateBindingMode.Independent
        };
    }

    private static Room? ResolveSeedDestinationRoom(
        Area area,
        Room sourceRoom,
        AreaRoomPlacement? sourcePlacement,
        IReadOnlyDictionary<Guid, AreaRoomPlacement> placementByRoomId,
        VerticalSeedPreference verticalSeedPreference)
    {
        if (sourcePlacement is null || verticalSeedPreference == VerticalSeedPreference.None)
        {
            return null;
        }

        var sourceCell = ToGridCell(sourcePlacement.X, sourcePlacement.Y);
        var candidates = (area.Rooms ?? new List<Room>())
            .Where(candidate => candidate.Id != sourceRoom.Id)
            .Select(candidate => new
            {
                Room = candidate,
                Placement = placementByRoomId.TryGetValue(candidate.Id, out var placement) ? placement : null
            })
            .Where(item => item.Placement is not null)
            .Select(item => new
            {
                item.Room,
                Placement = item.Placement!,
                IsSameCell = ToGridCell(item.Placement!.X, item.Placement.Y) == sourceCell,
                FloorDelta = item.Placement.FloorElevation - sourcePlacement.FloorElevation
            })
            .Where(item => verticalSeedPreference == VerticalSeedPreference.Up ? item.FloorDelta > 0 : item.FloorDelta < 0)
            .OrderByDescending(item => item.IsSameCell)
            .ThenBy(item => Math.Abs(item.FloorDelta))
            .ThenBy(item => item.Room.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Room)
            .ToList();

        return candidates.FirstOrDefault();
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

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = GetParentObject(current);
        }

        return null;
    }

    private static DependencyObject? GetParentObject(DependencyObject current)
    {
        if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        if (current is FrameworkContentElement frameworkContentElement)
        {
            return frameworkContentElement.Parent;
        }

        if (current is ContentElement contentElement)
        {
            return ContentOperations.GetParent(contentElement);
        }

        return null;
    }
}
