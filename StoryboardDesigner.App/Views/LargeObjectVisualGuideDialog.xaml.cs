using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        Action<int, int>? onFootprintChanged = null)
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

        _gridColumns = Math.Max(1, (projectCanvasWidth > 0 ? projectCanvasWidth : 800) / _cellSize);
        _gridRows = Math.Max(1, (projectCanvasHeight > 0 ? projectCanvasHeight : 600) / _cellSize);
        GetOrientedDimensions(out var orientedWidth, out var orientedHeight);
        _gridColumns = Math.Max(_gridColumns, orientedWidth);
        _gridRows = Math.Max(_gridRows, orientedHeight);
        RefreshEditors();
        RefreshPreview();
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
        GuideCellGrid.Columns = _gridColumns;
        GuideCellGrid.Rows = _gridRows;
        GuideCellGrid.Children.Clear();
        for (var i = 0; i < _gridColumns * _gridRows; i++)
        {
            GuideCellGrid.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(0.5),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)),
                Margin = new Thickness(0.5)
            });
        }

        var footprintWidthPixels = footprintWidth * _cellSize;
        var footprintHeightPixels = footprintHeight * _cellSize;
        var footprintLeft = (_gridColumns * _cellSize - footprintWidthPixels) / 2d;
        var footprintTop = (_gridRows * _cellSize - footprintHeightPixels) / 2d;
        FootprintOverlay.Width = footprintWidthPixels;
        FootprintOverlay.Height = footprintHeightPixels;
        FootprintOverlay.Margin = new Thickness(footprintLeft, footprintTop, 0, 0);

        var source = LoadImage(_variant.FullImagePath);
        GuideImage.Source = source;
        var scale = _variant.ImageScale <= 0 ? 1 : _variant.ImageScale;
        GuideImage.Width = (source?.PixelWidth ?? _cellSize) * scale;
        GuideImage.Height = (source?.PixelHeight ?? _cellSize) * scale;
        var rotation = double.IsFinite(_variant.ImageLocalAlignmentRotationDegrees) ? _variant.ImageLocalAlignmentRotationDegrees : 0;
        var offset = RotateOffset(
            double.IsFinite(_variant.ImageLocalAlignmentOffsetX) ? _variant.ImageLocalAlignmentOffsetX : 0,
            double.IsFinite(_variant.ImageLocalAlignmentOffsetY) ? _variant.ImageLocalAlignmentOffsetY : 0,
            rotation);
        GuideImage.Margin = new Thickness(footprintLeft + offset.X, footprintTop + offset.Y, 0, 0);
        GuideImage.RenderTransform = new RotateTransform(rotation);
        HeadingArrow.RenderTransform = new RotateTransform(DirectionToDegrees(_headingDirection));
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

    private static (double X, double Y) RotateOffset(double x, double y, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return (x * Math.Cos(radians) - y * Math.Sin(radians), x * Math.Sin(radians) + y * Math.Cos(radians));
    }

    private static bool TryParse(string value, out double result) => double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) && double.IsFinite(result);
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string NormalizeCardinalDirection(string? value) => value?.Trim().ToUpperInvariant() is "E" or "S" or "W" ? value.Trim().ToUpperInvariant() : "N";
    private static string NormalizeHeadingDirection(string? value) => value?.Trim().ToUpperInvariant() is "N" or "NE" or "E" or "SE" or "S" or "SW" or "W" or "NW" ? value.Trim().ToUpperInvariant() : "N";
    private static double DirectionToDegrees(string direction) => direction switch { "NE" => 45, "E" => 90, "SE" => 135, "S" => 180, "SW" => 225, "W" => 270, "NW" => 315, _ => 0 };
}
