using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class LargeObjectVisualGuideDialog : Window
{
    private readonly ObjectImageVariant _variant;
    private int _footprintWidthCells;
    private int _footprintHeightCells;
    private readonly string _footprintOrientation;
    private readonly string _headingDirection;
    private readonly int _cellSize;
    private readonly string _projectFilePath;
    private readonly string _preferredImageSourceBucket;
    private readonly Action<int, int>? _onFootprintChanged;
    private PointLight? _pointLight;
    private readonly Action<PointLight?>? _onPointLightChanged;
    private readonly List<StructuralPartPlacementEditItem> _structuralPartPlacements;
    private readonly ObservableCollection<StructuralPartRenderOrderEntry> _structuralPartRenderOrder = new();
    private readonly string _parentObjectName;
    private readonly bool _structuralPartRenderOrderIsReadOnly;
    private bool _hasEditedStructuralPartRenderOrder;
    private StructuralPartPlacementEditItem? _selectedStructuralPartPlacement;
    private bool _isDraggingStructuralPart;
    private System.Windows.Point _structuralPartDragStartPoint;
    private double _structuralPartDragStartOffsetX;
    private double _structuralPartDragStartOffsetY;
    private int _gridColumns;
    private int _gridRows;
    private bool _isRefreshingEditors;

    public LargeObjectVisualGuideDialog(
        ObjectImageVariant variant,
        int footprintWidthCells,
        int footprintHeightCells,
        string footprintOrientation,
        string headingDirection,
        int cellSize,
        int projectCanvasWidth,
        int projectCanvasHeight,
        string projectFilePath,
        string preferredImageSourceBucket,
        Action<int, int>? onFootprintChanged = null,
        PointLight? pointLight = null,
        Action<PointLight?>? onPointLightChanged = null,
        IReadOnlyList<StructuralPartPlacementEditItem>? structuralPartPlacements = null,
        string parentObjectName = "Parent",
        bool structuralPartRenderOrderIsReadOnly = false)
    {
        InitializeComponent();
        _variant = variant;
        _footprintWidthCells = Math.Max(1, footprintWidthCells);
        _footprintHeightCells = Math.Max(1, footprintHeightCells);
        _footprintOrientation = NormalizeCardinalDirection(footprintOrientation);
        _headingDirection = NormalizeHeadingDirection(headingDirection);
        _cellSize = Math.Max(1, cellSize);
        _projectFilePath = projectFilePath;
        _preferredImageSourceBucket = preferredImageSourceBucket;
        _onFootprintChanged = onFootprintChanged;
        _pointLight = LightingModelCloner.ClonePointLight(pointLight);
        _onPointLightChanged = onPointLightChanged;
        _structuralPartPlacements = CloneStructuralPartPlacements(structuralPartPlacements);
        _parentObjectName = string.IsNullOrWhiteSpace(parentObjectName) ? "Parent" : parentObjectName.Trim();
        _structuralPartRenderOrderIsReadOnly = structuralPartRenderOrderIsReadOnly;
        foreach (var entry in StructuralPartRelativeRenderOrderPolicy.BuildFrontToBack(_parentObjectName, _structuralPartPlacements))
        {
            _structuralPartRenderOrder.Add(entry);
        }
        StructuralPartRenderOrderListBox.ItemsSource = _structuralPartRenderOrder;
        MoveStructuralPartForwardButton.IsEnabled = !_structuralPartRenderOrderIsReadOnly;
        MoveStructuralPartBackwardButton.IsEnabled = !_structuralPartRenderOrderIsReadOnly;
        StructuralPartRenderOrderHintTextBlock.Text = _structuralPartRenderOrderIsReadOnly
            ? "This ordering is inherited from the Base Object and cannot be changed on a linked instance."
            : "Move structural parts around the parent row to place them in front of or behind the parent.";

        _gridColumns = Math.Max(1, (projectCanvasWidth > 0 ? projectCanvasWidth : 800) / _cellSize);
        _gridRows = Math.Max(1, (projectCanvasHeight > 0 ? projectCanvasHeight : 600) / _cellSize);
        GetOrientedDimensions(out var orientedWidth, out var orientedHeight);
        _gridColumns = Math.Max(_gridColumns, orientedWidth);
        _gridRows = Math.Max(_gridRows, orientedHeight);
        RefreshEditors();
        RefreshPreview();
    }

    public IReadOnlyList<StructuralPartPlacementEditItem> StructuralPartPlacements
    {
        get
        {
            var placements = CloneStructuralPartPlacements(_structuralPartPlacements);
            if (!_hasEditedStructuralPartRenderOrder)
            {
                return placements;
            }

            var ranks = StructuralPartRelativeRenderOrderPolicy.AssignRelativeRanks(_structuralPartRenderOrder);
            return placements.Select(item => item with
            {
                StructuralPartRelativeRenderOrder = ranks.TryGetValue(item.ObjectId, out var rank)
                    ? rank
                    : null
            }).ToList();
        }
    }

    private void RefreshEditors()
    {
        _isRefreshingEditors = true;
        VariantCaption.Text = $"Selected variant: {_variant.VariantName}";
        FootprintWidthTextBox.Text = _footprintWidthCells.ToString(CultureInfo.InvariantCulture);
        FootprintHeightTextBox.Text = _footprintHeightCells.ToString(CultureInfo.InvariantCulture);
        GridColumnsTextBox.Text = _gridColumns.ToString(CultureInfo.InvariantCulture);
        GridRowsTextBox.Text = _gridRows.ToString(CultureInfo.InvariantCulture);
        ScaleTextBox.Text = Format(_variant.ImageScale <= 0 ? 1 : _variant.ImageScale);
        RotationTextBox.Text = Format(double.IsFinite(_variant.ImageLocalAlignmentRotationDegrees) ? _variant.ImageLocalAlignmentRotationDegrees : 0);
        OffsetXTextBox.Text = Format(double.IsFinite(_variant.ImageLocalAlignmentOffsetX) ? _variant.ImageLocalAlignmentOffsetX : 0);
        OffsetYTextBox.Text = Format(double.IsFinite(_variant.ImageLocalAlignmentOffsetY) ? _variant.ImageLocalAlignmentOffsetY : 0);
        _isRefreshingEditors = false;
    }

    private void RefreshPreview()
    {
        GetOrientedDimensions(out var footprintWidth, out var footprintHeight);
        GuideSurfaceGrid.Width = _gridColumns * _cellSize;
        GuideSurfaceGrid.Height = _gridRows * _cellSize;
        GuideCellGrid.Width = GuideSurfaceGrid.Width;
        GuideCellGrid.Height = GuideSurfaceGrid.Height;
        GuideCellGrid.Children.Clear();

        var footprintWidthPixels = footprintWidth * _cellSize;
        var footprintHeightPixels = footprintHeight * _cellSize;
        var footprintLeft = (_gridColumns * _cellSize - footprintWidthPixels) / 2d;
        var footprintTop = (_gridRows * _cellSize - footprintHeightPixels) / 2d;
        DrawSurfaceGrid(footprintLeft, footprintTop);
        FootprintOverlay.Width = footprintWidthPixels;
        FootprintOverlay.Height = footprintHeightPixels;
        FootprintOverlay.Margin = new Thickness(footprintLeft, footprintTop, 0, 0);
        RefreshPreciseFootprintGrid(footprintLeft, footprintTop, footprintWidth, footprintHeight);

        var source = LoadImage(_variant.FullImagePath);
        GuideImage.Source = source;
        var scale = _variant.ImageScale <= 0 ? 1 : _variant.ImageScale;
        var rawWidth = source?.PixelWidth ?? _cellSize;
        var rawHeight = source?.PixelHeight ?? _cellSize;
        var renderedWidth = rawWidth * scale;
        var renderedHeight = rawHeight * scale;
        GuideImage.Width = renderedWidth;
        GuideImage.Height = renderedHeight;
        var rotation = double.IsFinite(_variant.ImageLocalAlignmentRotationDegrees) ? _variant.ImageLocalAlignmentRotationDegrees : 0;
        var placement = ComputeImagePlacement(
            rotation,
            double.IsFinite(_variant.ImageLocalAlignmentOffsetX) ? _variant.ImageLocalAlignmentOffsetX : 0,
            double.IsFinite(_variant.ImageLocalAlignmentOffsetY) ? _variant.ImageLocalAlignmentOffsetY : 0,
            footprintWidthPixels,
            footprintHeightPixels,
            renderedWidth,
            renderedHeight);
        Canvas.SetLeft(GuideImage, footprintLeft + placement.X);
        Canvas.SetTop(GuideImage, footprintTop + placement.Y);
        GuideImage.RenderTransform = new RotateTransform(rotation);
        BitmapBoundaryOverlay.Width = GuideImage.Width;
        BitmapBoundaryOverlay.Height = GuideImage.Height;
        Canvas.SetLeft(BitmapBoundaryOverlay, footprintLeft + placement.X);
        Canvas.SetTop(BitmapBoundaryOverlay, footprintTop + placement.Y);
        BitmapBoundaryOverlay.RenderTransform = new RotateTransform(rotation);
        BitmapBoundaryOverlay.Visibility = ShowBitmapBoundaryCheckBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdatePointLightMarker(footprintLeft, footprintTop, footprintWidthPixels, footprintHeightPixels);
        UpdateStructuralPartMarker(footprintLeft, footprintTop);
        HeadingArrow.RenderTransform = new RotateTransform(DirectionToDegrees(_headingDirection));
        ImageMetricsTextBlock.Text = source is null
            ? $"Bitmap: unavailable; rendered fallback: {FormatPixels(renderedWidth)} × {FormatPixels(renderedHeight)}px at scale {Format(scale)}."
            : $"Bitmap: {rawWidth} × {rawHeight}px; rendered: {FormatPixels(renderedWidth)} × {FormatPixels(renderedHeight)}px at scale {Format(scale)}.";
        StatusTextBlock.Text = $"Footprint: {footprintWidth} × {footprintHeight} cells; surface: {_gridColumns} × {_gridRows} cells; cell: {_cellSize}px.";
    }

    private void AlignmentTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isRefreshingEditors)
        {
            ApplyAlignmentFromEditors();
        }
    }

    private bool ApplyAlignmentFromEditors()
    {
        if (!TryParse(ScaleTextBox.Text, out var scale) || scale <= 0
            || !TryParse(RotationTextBox.Text, out var rotation)
            || !TryParse(OffsetXTextBox.Text, out var offsetX)
            || !TryParse(OffsetYTextBox.Text, out var offsetY))
        {
            return false;
        }

        _variant.ImageScale = scale;
        _variant.ImageLocalAlignmentRotationDegrees = rotation;
        _variant.ImageLocalAlignmentOffsetX = offsetX;
        _variant.ImageLocalAlignmentOffsetY = offsetY;
        RefreshPreview();
        return true;
    }

    private void DecreaseScaleButton_OnClick(object sender, RoutedEventArgs e) => NudgeScale(-0.01d);
    private void IncreaseScaleButton_OnClick(object sender, RoutedEventArgs e) => NudgeScale(0.01d);
    private void DecreaseRotationButton_OnClick(object sender, RoutedEventArgs e) => NudgeRotation(-15d);
    private void IncreaseRotationButton_OnClick(object sender, RoutedEventArgs e) => NudgeRotation(15d);
    private void DecreaseOffsetXButton_OnClick(object sender, RoutedEventArgs e) => NudgeOffsetX(-1d);
    private void IncreaseOffsetXButton_OnClick(object sender, RoutedEventArgs e) => NudgeOffsetX(1d);
    private void DecreaseOffsetYButton_OnClick(object sender, RoutedEventArgs e) => NudgeOffsetY(-1d);
    private void IncreaseOffsetYButton_OnClick(object sender, RoutedEventArgs e) => NudgeOffsetY(1d);

    private void NudgeScale(double delta)
    {
        var current = TryParse(ScaleTextBox.Text, out var value) && value > 0 ? value : 1d;
        ScaleTextBox.Text = Format(Math.Max(0.01d, Math.Round(current + delta, 3, MidpointRounding.AwayFromZero)));
    }

    private void NudgeRotation(double delta)
    {
        var current = TryParse(RotationTextBox.Text, out var value) ? value : 0d;
        RotationTextBox.Text = Format(Math.Round(current + delta, 3, MidpointRounding.AwayFromZero));
    }

    private void NudgeOffsetX(double delta)
    {
        var current = TryParse(OffsetXTextBox.Text, out var value) ? value : 0d;
        OffsetXTextBox.Text = Format(Math.Round(current + delta, 3, MidpointRounding.AwayFromZero));
    }

    private void NudgeOffsetY(double delta)
    {
        var current = TryParse(OffsetYTextBox.Text, out var value) ? value : 0d;
        OffsetYTextBox.Text = Format(Math.Round(current + delta, 3, MidpointRounding.AwayFromZero));
    }

    private void ApplyFootprintButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(FootprintWidthTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(FootprintHeightTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            || width < 1 || height < 1)
        {
            System.Windows.MessageBox.Show(this, "Footprint dimensions must be positive whole numbers.", "Large Object Visual Guide", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _footprintWidthCells = width;
        _footprintHeightCells = height;
        GetOrientedDimensions(out var orientedWidth, out var orientedHeight);
        _gridColumns = Math.Max(_gridColumns, orientedWidth);
        _gridRows = Math.Max(_gridRows, orientedHeight);
        _onFootprintChanged?.Invoke(width, height);
        RefreshEditors();
        RefreshPreview();
    }

    private void ApplyGridButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(GridColumnsTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns)
            || !int.TryParse(GridRowsTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows)
            || columns < 1 || rows < 1)
        {
            System.Windows.MessageBox.Show(this, "Grid dimensions must be positive whole numbers.", "Large Object Visual Guide", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        GetOrientedDimensions(out var footprintWidth, out var footprintHeight);
        if (columns < footprintWidth || rows < footprintHeight)
        {
            System.Windows.MessageBox.Show(this, "The visual surface cannot be smaller than the object footprint.", "Large Object Visual Guide", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _gridColumns = columns;
        _gridRows = rows;
        RefreshPreview();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void StructuralPartRenderOrderListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedEntry = StructuralPartRenderOrderListBox.SelectedItem as StructuralPartRenderOrderEntry;
        _selectedStructuralPartPlacement = selectedEntry is { IsParent: false, ObjectId: Guid objectId }
            ? _structuralPartPlacements.FirstOrDefault(placement => placement.ObjectId == objectId)
            : null;

        var canMove = !_structuralPartRenderOrderIsReadOnly
            && StructuralPartRenderOrderListBox.SelectedIndex >= 0;
        MoveStructuralPartForwardButton.IsEnabled = canMove && StructuralPartRenderOrderListBox.SelectedIndex > 0;
        MoveStructuralPartBackwardButton.IsEnabled = canMove
            && StructuralPartRenderOrderListBox.SelectedIndex < _structuralPartRenderOrder.Count - 1;

        if (_selectedStructuralPartPlacement is not { } selectedPart)
        {
            StructuralPartRenderOrderHintTextBlock.Text = "Select a child row, then click or drag on the footprint to position it.";
        }
        else
        {
            StructuralPartRenderOrderHintTextBlock.Text = $"Click to place {selectedPart.Name}, or drag its purple outline. Its footprint snaps to the parent-aligned grid.";
            GetOrientedDimensions(out var footprintWidth, out var footprintHeight);
            var partWidth = Math.Max(1, selectedPart.FootprintWidthCells);
            var partHeight = Math.Max(1, selectedPart.FootprintHeightCells);
            if (selectedPart.FootprintOrientation is "E" or "W")
            {
                (partWidth, partHeight) = (partHeight, partWidth);
            }

            if (partWidth > footprintWidth || partHeight > footprintHeight)
            {
                StructuralPartRenderOrderHintTextBlock.Text = "This child's footprint is larger than the parent footprint and cannot fit inside it.";
            }
        }

        GetOrientedDimensions(out var orientedWidth, out var orientedHeight);
        UpdateStructuralPartMarker(
            (_gridColumns - orientedWidth) * _cellSize / 2d,
            (_gridRows - orientedHeight) * _cellSize / 2d);
    }

    private void MoveStructuralPartForwardButton_OnClick(object sender, RoutedEventArgs e) => MoveSelectedRenderOrderRow(-1);

    private void MoveStructuralPartBackwardButton_OnClick(object sender, RoutedEventArgs e) => MoveSelectedRenderOrderRow(1);

    private void MoveSelectedRenderOrderRow(int delta)
    {
        if (_structuralPartRenderOrderIsReadOnly)
        {
            return;
        }

        var currentIndex = StructuralPartRenderOrderListBox.SelectedIndex;
        var targetIndex = currentIndex + delta;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _structuralPartRenderOrder.Count)
        {
            return;
        }

        var selectedEntry = _structuralPartRenderOrder[currentIndex];
        _structuralPartRenderOrder.Move(currentIndex, targetIndex);
        _hasEditedStructuralPartRenderOrder = true;
        var ranks = StructuralPartRelativeRenderOrderPolicy.AssignRelativeRanks(_structuralPartRenderOrder);
        for (var index = 0; index < _structuralPartRenderOrder.Count; index++)
        {
            var entry = _structuralPartRenderOrder[index];
            _structuralPartRenderOrder[index] = entry with
            {
                RelativeOrder = entry.IsParent
                    ? 0
                    : entry.ObjectId is Guid objectId && ranks.TryGetValue(objectId, out var rank)
                        ? rank
                        : null
            };
        }

        StructuralPartRenderOrderListBox.SelectedItem = _structuralPartRenderOrder.First(entry =>
            entry.IsParent == selectedEntry.IsParent && entry.ObjectId == selectedEntry.ObjectId);
        StructuralPartRenderOrderHintTextBlock.Text = "Saved ranks are relative to the parent: positive is in front, negative is behind.";
        GetOrientedDimensions(out var footprintWidth, out var footprintHeight);
        UpdateStructuralPartMarker(
            (_gridColumns - footprintWidth) * _cellSize / 2d,
            (_gridRows - footprintHeight) * _cellSize / 2d);
    }

    private void GuideSurfaceGrid_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        GetOrientedDimensions(out var footprintWidth, out var footprintHeight);
        var footprintWidthPixels = footprintWidth * _cellSize;
        var footprintHeightPixels = footprintHeight * _cellSize;
        var footprintLeft = (_gridColumns * _cellSize - footprintWidthPixels) / 2d;
        var footprintTop = (_gridRows * _cellSize - footprintHeightPixels) / 2d;
        var point = e.GetPosition(GuideSurfaceGrid);
        if (_selectedStructuralPartPlacement is { } selectedPart)
        {
            if (selectedPart.Placement.Mode != StructuralPartPlacementKind.ParentRelative)
            {
                StructuralPartRenderOrderHintTextBlock.Text = "This part uses fixed room coordinates. Change its placement mode to Relative to parent in Object Properties to place it here.";
                e.Handled = true;
                return;
            }

            var partWidth = Math.Max(1, selectedPart.FootprintWidthCells) * _cellSize;
            var partHeight = Math.Max(1, selectedPart.FootprintHeightCells) * _cellSize;
            if (selectedPart.FootprintOrientation is "E" or "W")
            {
                (partWidth, partHeight) = (partHeight, partWidth);
            }
            selectedPart.Placement.ParentRelativeOffsetX = SnapPartOffset(point.X - footprintLeft - partWidth / 2d);
            selectedPart.Placement.ParentRelativeOffsetY = SnapPartOffset(point.Y - footprintTop - partHeight / 2d);
            UpdateStructuralPartMarker(footprintLeft, footprintTop);
            StructuralPartRenderOrderHintTextBlock.Text = $"Placed {selectedPart.Name}. Select another child row to position it.";
            StatusTextBlock.Text = $"Footprint: {footprintWidth} × {footprintHeight} cells; placed {selectedPart.Name} at {selectedPart.Placement.ParentRelativeOffsetX:0} × {selectedPart.Placement.ParentRelativeOffsetY:0}px from the parent footprint's top-left.";
            e.Handled = true;
            return;
        }

        if (point.X < footprintLeft || point.Y < footprintTop || point.X > footprintLeft + footprintWidthPixels || point.Y > footprintTop + footprintHeightPixels)
        {
            return;
        }

        _pointLight ??= new PointLight();
        _pointLight.OffsetXPx = Math.Clamp(point.X - footprintLeft, 0, footprintWidthPixels);
        _pointLight.OffsetYPx = Math.Clamp(point.Y - footprintTop, 0, footprintHeightPixels);
        UpdatePointLightMarker(footprintLeft, footprintTop, footprintWidthPixels, footprintHeightPixels);
        StatusTextBlock.Text = $"Footprint: {footprintWidth} × {footprintHeight} cells; light origin: {_pointLight.OffsetXPx:0} × {_pointLight.OffsetYPx:0}px from top-left.";
        _onPointLightChanged?.Invoke(LightingModelCloner.ClonePointLight(_pointLight));
        e.Handled = true;
    }

    private void UpdateStructuralPartMarker(double footprintLeft, double footprintTop)
    {
        if (_selectedStructuralPartPlacement is not { Placement.Mode: StructuralPartPlacementKind.ParentRelative } part
            || part.Placement.ParentRelativeOffsetX is not double offsetX
            || part.Placement.ParentRelativeOffsetY is not double offsetY)
        {
            StructuralPartMarker.Visibility = Visibility.Collapsed;
            StructuralPartMarkerLabel.Visibility = Visibility.Collapsed;
            return;
        }

        var width = Math.Max(1, part.FootprintWidthCells) * _cellSize;
        var height = Math.Max(1, part.FootprintHeightCells) * _cellSize;
        if (part.FootprintOrientation is "E" or "W")
        {
            (width, height) = (height, width);
        }
        offsetX = SnapPartOffset(offsetX);
        offsetY = SnapPartOffset(offsetY);
        part.Placement.ParentRelativeOffsetX = offsetX;
        part.Placement.ParentRelativeOffsetY = offsetY;
        StructuralPartMarker.Width = width;
        StructuralPartMarker.Height = height;
        StructuralPartMarker.Margin = new Thickness(footprintLeft + offsetX, footprintTop + offsetY, 0, 0);
        StructuralPartMarkerLabel.Width = width;
        StructuralPartMarkerLabel.Height = height;
        StructuralPartMarkerLabel.Margin = new Thickness(footprintLeft + offsetX, footprintTop + offsetY, 0, 0);
        StructuralPartMarkerLabel.Text = part.Name;
        var relativeOrder = _hasEditedStructuralPartRenderOrder
            ? StructuralPartRelativeRenderOrderPolicy.AssignRelativeRanks(_structuralPartRenderOrder)
                .GetValueOrDefault(part.ObjectId)
            : part.StructuralPartRelativeRenderOrder;
        System.Windows.Controls.Panel.SetZIndex(StructuralPartMarker, StructuralPartRelativeRenderOrderPolicy.RendersInFrontOfParent(relativeOrder) ? 1 : -1);
        System.Windows.Controls.Panel.SetZIndex(StructuralPartMarkerLabel, 100);
        StructuralPartMarker.Visibility = Visibility.Visible;
        StructuralPartMarkerLabel.Visibility = Visibility.Visible;
    }

    private void RefreshPreciseFootprintGrid(double footprintLeft, double footprintTop, int footprintWidthCells, int footprintHeightCells)
    {
        FootprintCellLinesCanvas.Children.Clear();
        FootprintCellLinesCanvas.Width = footprintWidthCells * _cellSize;
        FootprintCellLinesCanvas.Height = footprintHeightCells * _cellSize;
        FootprintCellLinesCanvas.Margin = new Thickness(footprintLeft, footprintTop, 0, 0);
        FootprintCellLinesCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        FootprintCellLinesCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;

        var lineBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 51, 65, 85));
        lineBrush.Freeze();
        for (var column = 1; column < footprintWidthCells; column++)
        {
            var x = column * _cellSize;
            FootprintCellLinesCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = footprintHeightCells * _cellSize,
                Stroke = lineBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }

        for (var row = 1; row < footprintHeightCells; row++)
        {
            var y = row * _cellSize;
            FootprintCellLinesCanvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = footprintWidthCells * _cellSize,
                Y2 = y,
                Stroke = lineBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }
    }

    private void DrawSurfaceGrid(double originX, double originY)
    {
        var gridBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219));
        gridBrush.Freeze();
        var firstColumn = (int)Math.Ceiling(-originX / _cellSize);
        var lastColumn = (int)Math.Floor((GuideSurfaceGrid.Width - originX) / _cellSize);
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            var x = originX + column * _cellSize;
            GuideCellGrid.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = GuideSurfaceGrid.Height,
                Stroke = gridBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }

        var firstRow = (int)Math.Ceiling(-originY / _cellSize);
        var lastRow = (int)Math.Floor((GuideSurfaceGrid.Height - originY) / _cellSize);
        for (var row = firstRow; row <= lastRow; row++)
        {
            var y = originY + row * _cellSize;
            GuideCellGrid.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = GuideSurfaceGrid.Width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }
    }

    private void StructuralPartMarker_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button marker
            || _selectedStructuralPartPlacement is not { Placement.Mode: StructuralPartPlacementKind.ParentRelative } part)
        {
            return;
        }

        _isDraggingStructuralPart = true;
        _structuralPartDragStartPoint = e.GetPosition(GuideSurfaceGrid);
        _structuralPartDragStartOffsetX = part.Placement.ParentRelativeOffsetX ?? 0;
        _structuralPartDragStartOffsetY = part.Placement.ParentRelativeOffsetY ?? 0;
        marker.CaptureMouse();
        e.Handled = true;
    }

    private void StructuralPartMarker_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingStructuralPart || sender is not System.Windows.Controls.Button marker)
        {
            return;
        }

        var current = e.GetPosition(GuideSurfaceGrid);
        var deltaX = current.X - _structuralPartDragStartPoint.X;
        var deltaY = current.Y - _structuralPartDragStartPoint.Y;
        if (_selectedStructuralPartPlacement is { } part)
        {
            part.Placement.ParentRelativeOffsetX = SnapPartOffset(_structuralPartDragStartOffsetX + deltaX);
            part.Placement.ParentRelativeOffsetY = SnapPartOffset(_structuralPartDragStartOffsetY + deltaY);
            GetOrientedDimensions(out var footprintWidth, out var footprintHeight);
            UpdateStructuralPartMarker(
                (_gridColumns - footprintWidth) * _cellSize / 2d,
                (_gridRows - footprintHeight) * _cellSize / 2d);
        }

        e.Handled = true;
    }

    private void StructuralPartMarker_OnPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDraggingStructuralPart)
        {
            return;
        }

        if (_selectedStructuralPartPlacement is { } part)
        {
            StructuralPartRenderOrderHintTextBlock.Text = $"Placed {part.Name} at {part.Placement.ParentRelativeOffsetX:0} × {part.Placement.ParentRelativeOffsetY:0}px from the parent footprint's top-left.";
            GetOrientedDimensions(out var footprintWidth, out var footprintHeight);
            UpdateStructuralPartMarker(
                (_gridColumns - footprintWidth) * _cellSize / 2d,
                (_gridRows - footprintHeight) * _cellSize / 2d);
        }

        _isDraggingStructuralPart = false;
        if (sender is System.Windows.Controls.Button marker)
        {
            marker.ReleaseMouseCapture();
        }
        e.Handled = true;
    }

    private double SnapPartOffset(double offset)
    {
        return Math.Round(offset / _cellSize, MidpointRounding.AwayFromZero) * _cellSize;
    }

    private static List<StructuralPartPlacementEditItem> CloneStructuralPartPlacements(IEnumerable<StructuralPartPlacementEditItem>? source) =>
        (source ?? Array.Empty<StructuralPartPlacementEditItem>())
            .Select(item => item with
            {
                Placement = new StructuralPartPlacement
                {
                    Mode = item.Placement.Mode,
                    ParentRelativeOffsetX = item.Placement.ParentRelativeOffsetX,
                    ParentRelativeOffsetY = item.Placement.ParentRelativeOffsetY,
                    RoomCoordinateX = item.Placement.RoomCoordinateX,
                    RoomCoordinateY = item.Placement.RoomCoordinateY
                }
            })
            .ToList();

    private void UpdatePointLightMarker(double footprintLeft, double footprintTop, double footprintWidthPixels, double footprintHeightPixels)
    {
        if (_pointLight?.OffsetXPx is not double offsetX
            || _pointLight.OffsetYPx is not double offsetY
            || !double.IsFinite(offsetX)
            || !double.IsFinite(offsetY)
            || offsetX < 0
            || offsetY < 0
            || offsetX > footprintWidthPixels
            || offsetY > footprintHeightPixels)
        {
            PointLightMarker.Visibility = Visibility.Collapsed;
            return;
        }

        PointLightMarker.Margin = new Thickness(
            footprintLeft + offsetX - PointLightMarker.Width / 2d,
            footprintTop + offsetY - PointLightMarker.Height / 2d,
            0,
            0);
        PointLightMarker.Visibility = Visibility.Visible;
    }

    private void ShowBitmapBoundaryCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (BitmapBoundaryOverlay is not null)
        {
            BitmapBoundaryOverlay.Visibility = ShowBitmapBoundaryCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void GetOrientedDimensions(out int width, out int height)
    {
        if (_footprintOrientation is "E" or "W")
        {
            width = _footprintHeightCells;
            height = _footprintWidthCells;
        }
        else
        {
            width = _footprintWidthCells;
            height = _footprintHeightCells;
        }
    }

    private BitmapImage? LoadImage(string path)
    {
        var resolvedPath = DesignerImagePathResolver.ResolveForPreview(path, _projectFilePath, _preferredImageSourceBucket);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(resolvedPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    internal static (double X, double Y) ComputeImagePlacement(
        double localRotationDegrees,
        double localOffsetX,
        double localOffsetY,
        double footprintWidthPixels,
        double footprintHeightPixels,
        double renderedBitmapWidthPixels,
        double renderedBitmapHeightPixels)
    {
        return FoundationRenderablePlacementMath.ComputeIconLocalOffset(
            0d,
            localRotationDegrees,
            localOffsetX,
            localOffsetY,
            footprintWidthPixels,
            footprintHeightPixels,
            renderedBitmapWidthPixels,
            renderedBitmapHeightPixels);
    }

    private static bool TryParse(string value, out double result) => double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) && double.IsFinite(result);
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string FormatPixels(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string NormalizeCardinalDirection(string? value) => value?.Trim().ToUpperInvariant() is "E" or "S" or "W" ? value.Trim().ToUpperInvariant() : "N";
    private static string NormalizeHeadingDirection(string? value) => value?.Trim().ToUpperInvariant() is "N" or "NE" or "E" or "SE" or "S" or "SW" or "W" or "NW" ? value.Trim().ToUpperInvariant() : "N";
    private static double DirectionToDegrees(string direction) => direction switch { "NE" => 45, "E" => 90, "SE" => 135, "S" => 180, "SW" => 225, "W" => 270, "NW" => 315, _ => 0 };
}
