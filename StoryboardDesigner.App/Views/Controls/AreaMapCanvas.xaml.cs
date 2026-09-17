using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Models.Enums;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for AreaMapCanvas.xaml
/// </summary>
public partial class AreaMapCanvas : System.Windows.Controls.UserControl
{
    private ScrollViewer? _activeMapPanScrollViewer;
    private System.Windows.Point _mapPanStart;
    private double _mapPanStartHorizontalOffset;
    private double _mapPanStartVerticalOffset;
    private bool _isMapPanActive;
    private NavigationArrowViewModel? _pendingDestinationArrow;

    public AreaMapCanvas()
    {
        InitializeComponent();
    }

    private static IReadOnlyCollection<Direction10> ResolveAllowedTraversalDirectionsForEdit(TraversalConnection connection)
    {
        if (connection.BaseTraversalDirectionFromA is Direction10.Up or Direction10.Down)
        {
            return DirectionValues.All10;
        }

        return DirectionValues.DefaultTraversalDirections;
    }

    private MainWindowViewModel? GetViewModel() => Window.GetWindow(this)?.DataContext as MainWindowViewModel;

    private void AreaMapScrollViewer_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindAncestor<System.Windows.Controls.Primitives.ScrollBar>(source) is not null)
        {
            return;
        }

        if (HasAncestorDataContext<AreaRoomPlacementViewModel>(source) || HasAncestorDataContext<NavigationArrowViewModel>(source))
        {
            return;
        }

        _activeMapPanScrollViewer = scrollViewer;
        _mapPanStart = e.GetPosition(scrollViewer);
        _mapPanStartHorizontalOffset = scrollViewer.HorizontalOffset;
        _mapPanStartVerticalOffset = scrollViewer.VerticalOffset;
        _isMapPanActive = true;
        scrollViewer.CaptureMouse();
        scrollViewer.Cursor = System.Windows.Input.Cursors.SizeAll;
        e.Handled = true;
    }

    private void AreaMapScrollViewer_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isMapPanActive || _activeMapPanScrollViewer is null)
        {
            return;
        }

        var point = e.GetPosition(_activeMapPanScrollViewer);
        var deltaX = point.X - _mapPanStart.X;
        var deltaY = point.Y - _mapPanStart.Y;

        _activeMapPanScrollViewer.ScrollToHorizontalOffset(_mapPanStartHorizontalOffset - deltaX);
        _activeMapPanScrollViewer.ScrollToVerticalOffset(_mapPanStartVerticalOffset - deltaY);
        e.Handled = true;
    }

    private void AreaMapScrollViewer_OnPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isMapPanActive)
        {
            return;
        }

        StopMapPan();
        e.Handled = true;
    }

    private void NavigationCanvas_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel is null)
        {
            return;
        }

        if (!e.Data.GetDataPresent(typeof(RoomNodeViewModel)))
        {
            return;
        }

        if (sender is not Canvas canvas || e.Data.GetData(typeof(RoomNodeViewModel)) is not RoomNodeViewModel roomNode || canvas.DataContext is not AreaNavigationEditorTabViewModel editor)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        if (!ReferenceEquals(roomNode.Area, editor.Area))
        {
            System.Windows.MessageBox.Show(owner,
                "Only rooms from the same area can be placed on this navigation map.",
                "Area Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var point = e.GetPosition(canvas);

        var createAutoTraversals = editor.Area.RoomDropBehavior == AreaRoomDropBehavior.AutoBasicTraversals;
        var placed = editor.PlaceRoom(roomNode.Room, point.X - 64, point.Y - 48, out var validationMessage, createAutoTraversals);
        if (!placed && !string.IsNullOrWhiteSpace(validationMessage))
        {
            System.Windows.MessageBox.Show(owner, validationMessage, "Grid Validation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!placed)
        {
            return;
        }

        if (editor.Area.RoomDropBehavior == AreaRoomDropBehavior.PromptForWizard
            && editor.HasImmediateAdjacentRoom(roomNode.Room.Id))
        {
            var choice = System.Windows.MessageBox.Show(
                owner,
                "This room has adjacent neighbors. Run Traversal Wizard now for this room?\n\nChoose Yes to run the wizard, or No to keep no traversals on drop.",
                "Room Drop Behavior",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Yes)
            {
                viewModel.TryRunTraversalWizardForRoom(roomNode.Room);
                return;
            }
        }

        viewModel.NotifyProjectEdited();
    }

    private void NavigationArrow_OnPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ClearPendingDestinationArrow();
    }

    private void NavigationArrow_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel is null || e.ChangedButton != MouseButton.Left
            || sender is not FrameworkElement { DataContext: NavigationArrowViewModel arrow }
            || viewModel.SelectedAreaEditor is null)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        TraversalEditorDialog dialog;
        try
        {
            dialog = new TraversalEditorDialog(
                arrow.Connection,
                viewModel.SelectedAreaEditor.Area.Rooms,
                viewModel.SelectedAreaEditor.Area,
                ResolveAllowedTraversalDirectionsForEdit(arrow.Connection))
            {
                Owner = owner
            };
        }
        catch (Exception ex)
        {
            ShowTraversalDialogCrash("open traversal editor", ex);
            return;
        }

        bool? accepted;
        try
        {
            accepted = dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowTraversalDialogCrash("display traversal editor", ex);
            return;
        }

        if (accepted != true)
        {
            return;
        }

        var updated = viewModel.TryUpdateSelectedAreaTraversal(
            arrow.Connection,
            dialog.RoomAId,
            dialog.RoomBId,
            dialog.BaseTraversalDirectionFromA,
            dialog.TraversalAccessMode,
            dialog.OpenStateBindingMode,
            dialog.OpenStatePolicyFromA,
            dialog.OpenStatePolicyFromB,
            dialog.PresentationEffectKey);

        if (!updated)
        {
            System.Windows.MessageBox.Show(owner,
                "Could not update this traversal. Verify both rooms still exist and are distinct.",
                "Edit Traversal",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (viewModel.TryLinkTraversalDoors(
                arrow.Connection,
                dialog.FromRoomDoorObjectId,
                dialog.ToRoomDoorObjectId,
                out var warningMessage)
            && !string.IsNullOrWhiteSpace(warningMessage))
        {
            System.Windows.MessageBox.Show(owner,
                warningMessage,
                "Traversal Door Link Review",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        viewModel.NotifyProjectEdited();
        e.Handled = true;
    }

    private void NavigationArrowChangeDestination_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.DataContext is not NavigationArrowViewModel arrow)
        {
            return;
        }

        SetPendingDestinationArrow(arrow);
        System.Windows.MessageBox.Show(Window.GetWindow(this),
            "Change Destination mode is active. Click a room thumbnail on the map to set the new traversal endpoint.",
            "Change Traversal Destination",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void NavigationArrowDelete_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.DataContext is not NavigationArrowViewModel arrow)
        {
            return;
        }

        var viewModel = GetViewModel();
        if (viewModel is null)
        {
            return;
        }

        viewModel.RemoveNavigationLink(arrow.Connection);
        viewModel.NotifyProjectEdited();
    }

    internal bool TryApplyPendingDestinationForPlacement(Room room, MainWindowViewModel viewModel)
    {
        if (_pendingDestinationArrow is null)
        {
            return false;
        }

        if (_pendingDestinationArrow.SourceRoomId == room.Id)
        {
            System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                "Destination cannot be the same as the source room.",
                "Change Destination",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        }

        viewModel.ChangeNavigationDestination(_pendingDestinationArrow.Connection, _pendingDestinationArrow.SourceRoomId, room.Id);
        _pendingDestinationArrow = null;
        return true;
    }

    internal void SetPendingDestinationArrow(NavigationArrowViewModel arrow)
    {
        _pendingDestinationArrow = arrow;
    }

    internal bool ClearPendingDestinationArrow()
    {
        if (_pendingDestinationArrow is null)
        {
            return false;
        }

        _pendingDestinationArrow = null;
        return true;
    }

    private void StopMapPan()
    {
        if (_activeMapPanScrollViewer is not null)
        {
            _activeMapPanScrollViewer.ReleaseMouseCapture();
            _activeMapPanScrollViewer.ClearValue(CursorProperty);
        }

        _activeMapPanScrollViewer = null;
        _isMapPanActive = false;
    }

    private static bool HasAncestorDataContext<T>(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: T })
            {
                return true;
            }

            source = GetParentObject(source);
        }

        return false;
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
}
