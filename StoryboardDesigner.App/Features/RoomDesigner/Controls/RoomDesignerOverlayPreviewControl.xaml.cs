namespace StoryboardDesigner.App.Views.Controls;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using StoryboardDesigner.App.ViewModels;

/// <summary>
/// Interaction logic for RoomDesignerOverlayPreviewControl.xaml
/// </summary>
public partial class RoomDesignerOverlayPreviewControl : System.Windows.Controls.UserControl
{
    private bool _isDragging;
    private Point _dragStart;
    private double _originX;
    private double _originY;
    private int _originZ;
    private Button? _dragButton;
    private ContentPresenter? _dragPresenter;
    private RoomDesignerPreviewObjectViewModel? _dragObject;
    private RoomDesignerTabViewModel? _observedTab;

    public RoomDesignerOverlayPreviewControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        PreviewLayerGrid.SizeChanged += PreviewLayerGrid_OnSizeChanged;
    }

    private void RoomObjectThumb_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();

        if (sender is not Button button || button.DataContext is not RoomDesignerPreviewObjectViewModel previewObject)
        {
            return;
        }

        if (DataContext is RoomDesignerTabViewModel tab)
        {
            tab.SelectedRoomChildObject = previewObject;
        }

        var presenter = FindAncestor<ContentPresenter>(button);
        if (presenter is null)
        {
            return;
        }

        _isDragging = true;
        _dragStart = e.GetPosition(PreviewLayerGrid);
        _originX = previewObject.PositionX;
        _originY = previewObject.PositionY;
        _originZ = Panel.GetZIndex(presenter);
        _dragButton = button;
        _dragPresenter = presenter;
        _dragObject = previewObject;

        Panel.SetZIndex(presenter, int.MaxValue);
        button.CaptureMouse();
        e.Handled = true;
    }

    private void RoomObjectThumb_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _dragPresenter is null)
        {
            return;
        }

        var current = e.GetPosition(PreviewLayerGrid);
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;

        _dragPresenter.RenderTransform = new TranslateTransform(dx, dy);
        e.Handled = true;
    }

    private void RoomObjectThumb_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        CommitDrag(sender as Button, e.GetPosition(PreviewLayerGrid));
        e.Handled = true;
    }

    private void CommitDrag(Button? sourceButton, Point current)
    {
        if (_dragObject is null)
        {
            ResetDragState();
            return;
        }

        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;

        var dragWidth = sourceButton?.ActualWidth ?? _dragPresenter?.ActualWidth ?? 0;
        var dragHeight = sourceButton?.ActualHeight ?? _dragPresenter?.ActualHeight ?? 0;

        var previewWidth = DataContext is RoomDesignerTabViewModel tabForBounds && tabForBounds.DesignerCanvasWidth > 0
            ? tabForBounds.DesignerCanvasWidth
            : PreviewLayerGrid.ActualWidth;
        var previewHeight = DataContext is RoomDesignerTabViewModel tabForBoundsHeight && tabForBoundsHeight.DesignerCanvasHeight > 0
            ? tabForBoundsHeight.DesignerCanvasHeight
            : PreviewLayerGrid.ActualHeight;

        var maxX = Math.Max(0, previewWidth - dragWidth);
        var maxY = Math.Max(0, previewHeight - dragHeight);

        var nextX = Math.Clamp(_originX + dx, 0, maxX);
        var nextY = Math.Clamp(_originY + dy, 0, maxY);

        if (DataContext is RoomDesignerTabViewModel tab)
        {
            nextX = Math.Clamp(tab.SnapCoordinate(nextX), 0, maxX);
            nextY = Math.Clamp(tab.SnapCoordinate(nextY), 0, maxY);
            tab.TryCommitRoomChildObjectMove(_dragObject, _originX, _originY, nextX, nextY);
        }
        else
        {
            _dragObject.PositionX = nextX;
            _dragObject.PositionY = nextY;
        }

        ResetDragState();
    }

    private void ResetDragState()
    {
        if (_dragButton is not null)
        {
            _dragButton.ReleaseMouseCapture();
        }

        if (_dragPresenter is not null)
        {
            Panel.SetZIndex(_dragPresenter, _originZ);
            _dragPresenter.RenderTransform = Transform.Identity;
        }

        _isDragging = false;
        _dragButton = null;
        _dragPresenter = null;
        _dragObject = null;
    }

    private static T? FindAncestor<T>(DependencyObject? start)
        where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not RoomDesignerTabViewModel tab || tab.SelectedRoomChildObject is null)
        {
            return;
        }

        var step = tab.GetKeyboardNudgeStep((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
        var deltaX = 0.0;
        var deltaY = 0.0;

        switch (e.Key)
        {
            case Key.Left:
                deltaX = -step;
                break;
            case Key.Right:
                deltaX = step;
                break;
            case Key.Up:
                deltaY = -step;
                break;
            case Key.Down:
                deltaY = step;
                break;
            default:
                return;
        }

        var presenter = RoomObjectPreviewItemsControl.ItemContainerGenerator.ContainerFromItem(tab.SelectedRoomChildObject) as ContentPresenter;
        var objectWidth = presenter?.ActualWidth ?? tab.SelectedRoomChildObject.FootprintWidthPixels;
        var objectHeight = presenter?.ActualHeight ?? tab.SelectedRoomChildObject.FootprintHeightPixels;
        var previewWidth = tab.DesignerCanvasWidth > 0 ? tab.DesignerCanvasWidth : PreviewLayerGrid.ActualWidth;
        var previewHeight = tab.DesignerCanvasHeight > 0 ? tab.DesignerCanvasHeight : PreviewLayerGrid.ActualHeight;

        if (tab.NudgeSelectedRoomChildObject(
                deltaX,
                deltaY,
            previewWidth,
            previewHeight,
                objectWidth,
                objectHeight))
        {
            e.Handled = true;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshGridOverlay();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_observedTab is not null)
        {
            _observedTab.PropertyChanged -= OnObservedTabPropertyChanged;
        }

        _observedTab = e.NewValue as RoomDesignerTabViewModel;
        if (_observedTab is not null)
        {
            _observedTab.PropertyChanged += OnObservedTabPropertyChanged;
        }

        RefreshGridOverlay();
    }

    private void OnObservedTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomDesignerTabViewModel.IsGridVisible)
            || e.PropertyName == nameof(RoomDesignerTabViewModel.RoomGridCellSize)
            || e.PropertyName == nameof(RoomDesignerTabViewModel.DesignerCanvasWidth)
            || e.PropertyName == nameof(RoomDesignerTabViewModel.DesignerCanvasHeight))
        {
            RefreshGridOverlay();
        }
    }

    private void PreviewLayerGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshGridOverlay();
    }

    private void RefreshGridOverlay()
    {
        if (GridOverlayCanvas is null)
        {
            return;
        }

        GridOverlayCanvas.Children.Clear();

        if (DataContext is not RoomDesignerTabViewModel tab || !tab.IsGridVisible)
        {
            return;
        }

        var cellSize = Math.Max(1, tab.RoomGridCellSize);
        var width = PreviewLayerGrid.ActualWidth;
        var height = PreviewLayerGrid.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        GridOverlayCanvas.Width = width;
        GridOverlayCanvas.Height = height;

        var stroke = new SolidColorBrush(Color.FromArgb(90, 148, 163, 184));
        stroke.Freeze();

        for (var x = cellSize; x < width; x += cellSize)
        {
            GridOverlayCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = stroke,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }

        for (var y = cellSize; y < height; y += cellSize)
        {
            GridOverlayCanvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = stroke,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }
    }
}
