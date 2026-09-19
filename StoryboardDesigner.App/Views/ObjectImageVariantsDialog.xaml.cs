using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.Config;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Views;

public partial class ObjectImageVariantsDialog : Window
{
    private static readonly string[] HeadingOrder = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
    private const int GuideGridCellCount = 7;
    private const double VariantScaleNudgeStep = 0.01d;
    private const double VariantScaleNudgeMinimum = 0.01d;
    private readonly string _projectFilePath;
    private readonly string _preferredImageSourceBucket;
    private readonly int _projectRoomGridCellSize;
    private readonly int _projectRoomCanvasWidth;
    private readonly int _projectRoomCanvasHeight;
    private double _imageRotationDegrees;
    private readonly List<ObjectImageVariant> _imageVariants;
    private readonly IReadOnlyList<string> _chooserReferenceTokens;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _chooserVariableChoices;
    private readonly PropertyResolutionScope _chooserVariableScope;
    private string _imageVariantChooserScript;
    private bool _isMovable;
    private bool _isMovableDefaultValue;
    private RuntimeObjectSpatialTypes _spatialType;
    private int _stackOrder;
    private int _footprintWidthCells;
    private int _footprintHeightCells;
    private string _footprintOrientation;
    private string _headingDirection;
    private int _objectHeightUnits;
    private int _heightInRoom;
    private double? _stackScaleStepOverride;
    private double? _minStackScaleOverride;
    private bool _isUpdatingGuideVariantFitEditors;
    private const int CompactGuideMaximumFootprintCells = 7;

    public ObjectImageVariantsDialog(ObjectImageVariantsEditRequest initial)
    {
        InitializeComponent();

        _projectFilePath = initial.ProjectFilePath;
        _preferredImageSourceBucket = initial.PreferredImageSourceBucket;
        _projectRoomGridCellSize = initial.ProjectRoomGridCellSize > 0 ? initial.ProjectRoomGridCellSize : 40;
        _projectRoomCanvasWidth = initial.ProjectRoomCanvasWidth > 0 ? initial.ProjectRoomCanvasWidth : 800;
        _projectRoomCanvasHeight = initial.ProjectRoomCanvasHeight > 0 ? initial.ProjectRoomCanvasHeight : 600;
        _imageRotationDegrees = initial.ImageRotationDegrees;
        _imageVariants = NormalizeImageVariants(initial.ImageVariants);
        _chooserReferenceTokens = initial.ImageVariantChooserReferenceTokens ?? Array.Empty<string>();
        _chooserVariableChoices = initial.ImageVariantChooserVariableChoices ?? Array.Empty<GamePropertyChoiceItem>();
        _chooserVariableScope = initial.ImageVariantChooserVariableScope;
        _imageVariantChooserScript = initial.ImageVariantChooserScript ?? string.Empty;
        _isMovable = initial.IsMovable;
        _isMovableDefaultValue = initial.IsMovableDefaultValue;
        _spatialType = initial.SpatialType;
        _stackOrder = initial.StackGroup < 0 ? 0 : initial.StackGroup;
        _footprintWidthCells = initial.FootprintWidthCells < 1 ? 1 : initial.FootprintWidthCells;
        _footprintHeightCells = initial.FootprintHeightCells < 1 ? 1 : initial.FootprintHeightCells;
        _footprintOrientation = NormalizeCardinalDirection(initial.FootprintOrientation);
        _headingDirection = NormalizeHeadingDirection(initial.HeadingDirection);
        _objectHeightUnits = initial.ObjectHeightUnits < 0 ? 0 : initial.ObjectHeightUnits;
        _heightInRoom = initial.HeightInRoom < 0 ? 0 : initial.HeightInRoom;
        _stackScaleStepOverride = SanitizeOptionalStackScaleStep(initial.StackScaleStepOverride);
        _minStackScaleOverride = SanitizeOptionalMinStackScale(initial.MinStackScaleOverride);

        IsMovableCheckBox.IsChecked = _isMovable;
        SetBooleanComboSelection(MovableDefaultValueComboBox, _isMovableDefaultValue);
        SetStringComboSelection(SpatialTypeComboBox, _spatialType.ToString(), nameof(RuntimeObjectSpatialTypes.SolidObject));
        FootprintWidthTextBox.Text = _footprintWidthCells.ToString(CultureInfo.InvariantCulture);
        FootprintHeightTextBox.Text = _footprintHeightCells.ToString(CultureInfo.InvariantCulture);
        SetStringComboSelection(FootprintOrientationComboBox, _footprintOrientation, "N");
        SetStringComboSelection(HeadingDirectionComboBox, _headingDirection, "N");
        StackOrderTextBox.Text = _stackOrder.ToString(CultureInfo.InvariantCulture);
        ObjectHeightUnitsTextBox.Text = _objectHeightUnits.ToString(CultureInfo.InvariantCulture);
        HeightInRoomTextBox.Text = _heightInRoom.ToString(CultureInfo.InvariantCulture);
        StackScaleStepOverrideTextBox.Text = FormatOptionalDouble(_stackScaleStepOverride);
        MinStackScaleOverrideTextBox.Text = FormatOptionalDouble(_minStackScaleOverride);
        RefreshSpatialTypeUiState();
        ApplyGuideSurfaceSizing();
        RefreshChooserScriptPreview();

        RefreshVariantList();
        RefreshGuidePreview();
    }

    public IReadOnlyList<ObjectImageVariant> ImageVariants => _imageVariants;

    public string ImageVariantChooserScript => _imageVariantChooserScript;

    public double ImageRotationDegrees => _imageRotationDegrees;

    public bool IsMovable => _isMovable;

    public bool IsMovableDefaultValue => _isMovableDefaultValue;

    public RuntimeObjectSpatialTypes SpatialType => _spatialType;

    public int StackGroup => _stackOrder;

    public int FootprintWidthCells => _footprintWidthCells;

    public int FootprintHeightCells => _footprintHeightCells;

    public string FootprintOrientation => _footprintOrientation;

    public string HeadingDirection => _headingDirection;

    public int ObjectHeightUnits => _objectHeightUnits;

    public int HeightInRoom => _heightInRoom;

    public double? StackScaleStepOverride => _stackScaleStepOverride;

    public double? MinStackScaleOverride => _minStackScaleOverride;

    private double GuideSurfaceSizePixels => GuideGridCellCount * _projectRoomGridCellSize;

    private void ApplyGuideSurfaceSizing()
    {
        if (GuideSurfaceGrid is null)
        {
            return;
        }

        var guideSurfaceSize = GuideSurfaceSizePixels;
        GuideSurfaceGrid.Width = guideSurfaceSize;
        GuideSurfaceGrid.Height = guideSurfaceSize;
    }

    private static List<ObjectImageVariant> NormalizeImageVariants(IReadOnlyList<ObjectImageVariant>? variants)
    {
        var normalized = (variants ?? Array.Empty<ObjectImageVariant>())
            .Where(static variant => variant is not null && !string.IsNullOrWhiteSpace(variant.VariantName))
            .Select(variant => new ObjectImageVariant
            {
                VariantName = variant.VariantName.Trim(),
                FullImagePath = variant.FullImagePath?.Trim() ?? string.Empty,
                ImageLocalAlignmentRotationDegrees = double.IsFinite(variant.ImageLocalAlignmentRotationDegrees) ? variant.ImageLocalAlignmentRotationDegrees : 0,
                ImageLocalAlignmentOffsetX = double.IsFinite(variant.ImageLocalAlignmentOffsetX) ? variant.ImageLocalAlignmentOffsetX : 0,
                ImageLocalAlignmentOffsetY = double.IsFinite(variant.ImageLocalAlignmentOffsetY) ? variant.ImageLocalAlignmentOffsetY : 0,
                ImageScale = variant.ImageScale <= 0 ? 1 : variant.ImageScale,
                IsDefault = variant.IsDefault
            })
            .GroupBy(static variant => variant.VariantName, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        if (normalized.Count == 0)
        {
            normalized.Add(new ObjectImageVariant
            {
                VariantName = "default",
                FullImagePath = string.Empty,
                ImageLocalAlignmentRotationDegrees = 0,
                ImageLocalAlignmentOffsetX = 0,
                ImageLocalAlignmentOffsetY = 0,
                ImageScale = 1,
                IsDefault = true
            });
            return normalized;
        }

        var defaultIndex = normalized.FindIndex(static variant => variant.IsDefault);
        if (defaultIndex < 0)
        {
            normalized[0].IsDefault = true;
            return normalized;
        }

        for (var index = 0; index < normalized.Count; index++)
        {
            normalized[index].IsDefault = index == defaultIndex;
        }

        return normalized;
    }

    private void RefreshVariantList(string? selectVariantName = null)
    {
        ImageVariantListBox.ItemsSource = null;
        ImageVariantListBox.ItemsSource = _imageVariants;

        ObjectImageVariant? selected = null;
        if (!string.IsNullOrWhiteSpace(selectVariantName))
        {
            selected = _imageVariants.FirstOrDefault(variant => string.Equals(variant.VariantName, selectVariantName, StringComparison.OrdinalIgnoreCase));
        }

        selected ??= _imageVariants.FirstOrDefault(static variant => variant.IsDefault) ?? _imageVariants.First();
        ImageVariantListBox.SelectedItem = selected;
        VariantNameTextBox.Text = selected.VariantName;
        UpdateVariantActionButtonState();
        UpdateChooserValidationHint();
        RefreshAppearanceThumbnail(selected.FullImagePath);
        UpdateGuideVariantFitEditors(selected);
    }

    private ObjectImageVariant? GetSelectedVariant()
    {
        return ImageVariantListBox.SelectedItem as ObjectImageVariant;
    }

    private void AddVariantButton_OnClick(object sender, RoutedEventArgs e)
    {
        var variant = new ObjectImageVariant
        {
            VariantName = GenerateUniqueVariantName(),
            FullImagePath = string.Empty,
            ImageLocalAlignmentRotationDegrees = 0,
            ImageLocalAlignmentOffsetX = 0,
            ImageLocalAlignmentOffsetY = 0,
            ImageScale = 1,
            IsDefault = false
        };

        _imageVariants.Add(variant);
        RefreshVariantList(variant.VariantName);
    }

    private void RemoveVariantButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            return;
        }

        if (_imageVariants.Count <= 1)
        {
            System.Windows.MessageBox.Show(this, "At least one image variant is required.", "Object Images", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _imageVariants.Remove(selected);
        EnsureSingleDefault();
        RefreshVariantList();
    }

    private void SetDefaultVariantButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            return;
        }

        foreach (var variant in _imageVariants)
        {
            variant.IsDefault = ReferenceEquals(variant, selected);
        }

        RefreshVariantList(selected.VariantName);
    }

    private void RenameVariantButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            return;
        }

        var requestedName = VariantNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            System.Windows.MessageBox.Show(this, "Variant name is required.", "Object Images", MessageBoxButton.OK, MessageBoxImage.Information);
            VariantNameTextBox.Focus();
            return;
        }

        if (_imageVariants.Any(variant => !ReferenceEquals(variant, selected)
                                         && string.Equals(variant.VariantName, requestedName, StringComparison.OrdinalIgnoreCase)))
        {
            System.Windows.MessageBox.Show(this, "Variant names must be unique within an object.", "Object Images", MessageBoxButton.OK, MessageBoxImage.Information);
            VariantNameTextBox.Focus();
            VariantNameTextBox.SelectAll();
            return;
        }

        selected.VariantName = requestedName;
        RefreshVariantList(selected.VariantName);
        RefreshGuidePreview();
    }

    private void MoveVariantUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedVariant(-1);
    }

    private void MoveVariantDownButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedVariant(1);
    }

    private void MoveSelectedVariant(int offset)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            return;
        }

        var currentIndex = _imageVariants.FindIndex(variant => ReferenceEquals(variant, selected));
        if (currentIndex < 0)
        {
            return;
        }

        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= _imageVariants.Count)
        {
            return;
        }

        _imageVariants.RemoveAt(currentIndex);
        _imageVariants.Insert(targetIndex, selected);
        RefreshVariantList(selected.VariantName);
    }

    private void EditSelectedAppearance_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            return;
        }

        var dialog = new ObjectAppearanceDialog(
            new ObjectAppearanceEditRequest(
                selected.FullImagePath,
                string.Empty,
                string.Empty,
                selected.ImageLocalAlignmentRotationDegrees,
                selected.ImageScale <= 0 ? 1 : selected.ImageScale,
                selected.ImageLocalAlignmentOffsetX,
                selected.ImageLocalAlignmentOffsetY),
            _projectFilePath,
            _preferredImageSourceBucket)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        selected.FullImagePath = dialog.Values.FullImagePath;
        selected.ImageLocalAlignmentRotationDegrees = dialog.Values.RotationDegrees;
        selected.ImageScale = dialog.Values.Scale <= 0 ? 1 : dialog.Values.Scale;
        selected.ImageLocalAlignmentOffsetX = dialog.Values.LocalOffsetX;
        selected.ImageLocalAlignmentOffsetY = dialog.Values.LocalOffsetY;
        RefreshVariantList(selected.VariantName);
        RefreshGuidePreview();
    }

    private void ImageVariantListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            VariantNameTextBox.Text = string.Empty;
            return;
        }

        VariantNameTextBox.Text = selected.VariantName;
        UpdateVariantActionButtonState();
        RefreshAppearanceThumbnail(selected.FullImagePath);
        RefreshGuidePreview();
    }

    private void FootprintOrientationComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _footprintOrientation = NormalizeCardinalDirection(GetSelectedComboString(FootprintOrientationComboBox, "N"));
        RefreshGuidePreview();
    }

    private void HeadingDirectionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _headingDirection = NormalizeHeadingDirection(GetSelectedComboString(HeadingDirectionComboBox, "N"));
        RefreshGuidePreview();
    }

    private void SpatialTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _spatialType = ParseSpatialType(GetSelectedComboString(SpatialTypeComboBox, nameof(RuntimeObjectSpatialTypes.SolidObject)));
        RefreshSpatialTypeUiState();
    }

    private void RefreshSpatialTypeUiState()
    {
        var isPassive = _spatialType == RuntimeObjectSpatialTypes.PassiveObject;
        StackOrderTextBox.IsEnabled = !isPassive;
        if (isPassive)
        {
            StackOrderTextBox.Text = "0";
        }
    }

    private void RotateLeftButton_OnClick(object sender, RoutedEventArgs e)
    {
        RotateHeading(-1);
    }

    private void RotateRightButton_OnClick(object sender, RoutedEventArgs e)
    {
        RotateHeading(1);
    }

    private void RotateHeading(int offset)
    {
        var index = Array.FindIndex(HeadingOrder, direction => string.Equals(direction, _headingDirection, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            index = 0;
        }

        index = (index + offset) % HeadingOrder.Length;
        if (index < 0)
        {
            index += HeadingOrder.Length;
        }

        _headingDirection = HeadingOrder[index];
        SetStringComboSelection(HeadingDirectionComboBox, _headingDirection, "N");
        RefreshGuidePreview();
    }

    private void EditChooserScriptButton_OnClick(object sender, RoutedEventArgs e)
    {
        var availableVariantNames = _imageVariants
            .Select(static variant => variant.VariantName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        var dialog = new ImageVariantChooserScriptEditorDialog(
            _imageVariantChooserScript,
            _chooserReferenceTokens,
            _chooserVariableChoices,
            _chooserVariableScope,
            availableVariantNames)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _imageVariantChooserScript = dialog.ScriptText.Trim();
        RefreshChooserScriptPreview();
        UpdateChooserValidationHint();
    }

    private void RefreshChooserScriptPreview()
    {
        ImageVariantChooserScriptTextBox.Text = _imageVariantChooserScript;
    }

    private void UpdateChooserValidationHint()
    {
        var chooser = _imageVariantChooserScript;
        var availableNames = new HashSet<string>(_imageVariants.Select(static variant => variant.VariantName), StringComparer.OrdinalIgnoreCase);
        var referencedNames = ExtractReferencedVariantNames(chooser);
        var unknown = referencedNames.Where(name => !availableNames.Contains(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        if (unknown.Count == 0)
        {
            ImageVariantValidationHintTextBlock.Visibility = Visibility.Collapsed;
            ImageVariantValidationHintTextBlock.Text = string.Empty;
            return;
        }

        ImageVariantValidationHintTextBlock.Text = $"Chooser references unknown variants: {string.Join(", ", unknown)}.";
        ImageVariantValidationHintTextBlock.Visibility = Visibility.Visible;
    }

    private static HashSet<string> ExtractReferencedVariantNames(string chooser)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(chooser))
        {
            return names;
        }

        foreach (Match match in Regex.Matches(chooser, "'([^'\\r\\n]+)'") )
        {
            var candidate = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                names.Add(candidate);
            }
        }

        foreach (Match match in Regex.Matches(chooser, "\"([^\"\\r\\n]+)\"") )
        {
            var candidate = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                names.Add(candidate);
            }
        }

        return names;
    }

    private void UpdateVariantActionButtonState()
    {
        var selected = GetSelectedVariant();
        var hasSelection = selected is not null;
        var selectedIndex = hasSelection ? _imageVariants.FindIndex(variant => ReferenceEquals(variant, selected)) : -1;

        RemoveVariantButton.IsEnabled = hasSelection && _imageVariants.Count > 1;
        SetDefaultVariantButton.IsEnabled = hasSelection;
        RenameVariantButton.IsEnabled = hasSelection;
        EditSelectedAppearanceButton.IsEnabled = hasSelection;
        MoveVariantUpButton.IsEnabled = hasSelection && selectedIndex > 0;
        MoveVariantDownButton.IsEnabled = hasSelection && selectedIndex >= 0 && selectedIndex < _imageVariants.Count - 1;
        GuideVariantScaleTextBox.IsEnabled = hasSelection;
        GuideVariantRotationTextBox.IsEnabled = hasSelection;
        DecreaseVariantScaleButton.IsEnabled = hasSelection;
        IncreaseVariantScaleButton.IsEnabled = hasSelection;
        DecreaseVariantRotationButton.IsEnabled = hasSelection;
        IncreaseVariantRotationButton.IsEnabled = hasSelection;
        ApplyVariantFitButton.IsEnabled = hasSelection;
    }

    private static BitmapImage? LoadThumbnail(string path, string? projectFilePath, string? preferredSourceBucket)
    {
        var resolvedPath = DesignerImagePathResolver.ResolveForPreview(path, projectFilePath, preferredSourceBucket);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

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
        catch
        {
            return null;
        }
    }

    private void RefreshAppearanceThumbnail(string path)
    {
        var source = LoadThumbnail(path, _projectFilePath, _preferredImageSourceBucket);
        AppearanceThumbnailImage.Source = source;
        AppearanceNoImageTextBlock.Visibility = source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshGuidePreview()
    {
        var useLargeGuide = RequiresLargeGuide();
        GuideSurfaceGrid.Visibility = useLargeGuide ? Visibility.Collapsed : Visibility.Visible;
        LargeGuidePromptPanel.Visibility = useLargeGuide ? Visibility.Visible : Visibility.Collapsed;
        CompactGuideVariantFitPanel.Visibility = useLargeGuide ? Visibility.Collapsed : Visibility.Visible;
        DrawFootprintGuide();
        UpdateGuideImageOverlay();
        UpdateHeadingArrowTransform();
        UpdateGuideLegend();
        UpdateGuideVariantFitEditors(GetSelectedVariant());
    }

    private bool RequiresLargeGuide()
    {
        var width = ParsePositiveIntOrFallback(FootprintWidthTextBox.Text, _footprintWidthCells);
        var height = ParsePositiveIntOrFallback(FootprintHeightTextBox.Text, _footprintHeightCells);
        return width > CompactGuideMaximumFootprintCells || height > CompactGuideMaximumFootprintCells;
    }

    private void OpenLargeGuideButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            return;
        }

        var footprintWidth = ParsePositiveIntOrFallback(FootprintWidthTextBox.Text, _footprintWidthCells);
        var footprintHeight = ParsePositiveIntOrFallback(FootprintHeightTextBox.Text, _footprintHeightCells);
        var footprintOrientation = NormalizeCardinalDirection(GetSelectedComboString(FootprintOrientationComboBox, _footprintOrientation));
        var headingDirection = NormalizeHeadingDirection(GetSelectedComboString(HeadingDirectionComboBox, _headingDirection));

        var dialog = new LargeObjectVisualGuideDialog(
            selected,
            footprintWidth,
            footprintHeight,
            footprintOrientation,
            headingDirection,
            _projectRoomGridCellSize,
            _projectRoomCanvasWidth,
            _projectRoomCanvasHeight,
            _projectFilePath,
            _preferredImageSourceBucket,
            (width, height) =>
            {
                _footprintWidthCells = width;
                _footprintHeightCells = height;
                FootprintWidthTextBox.Text = width.ToString(CultureInfo.InvariantCulture);
                FootprintHeightTextBox.Text = height.ToString(CultureInfo.InvariantCulture);
                RefreshGuidePreview();
            })
        {
            Owner = this
        };

        dialog.Closed += (_, _) =>
        {
            RefreshVariantList(selected.VariantName);
            RefreshGuidePreview();
        };
        dialog.Show();
    }

    private void UpdateGuideVariantFitEditors(ObjectImageVariant? selected)
    {
        _isUpdatingGuideVariantFitEditors = true;

        if (selected is null)
        {
            GuideVariantFitCaptionTextBlock.Text = "Selected variant: none";
            GuideVariantScaleTextBox.Text = string.Empty;
            GuideVariantRotationTextBox.Text = string.Empty;
            GuideVariantOffsetXTextBox.Text = string.Empty;
            GuideVariantOffsetYTextBox.Text = string.Empty;
            _isUpdatingGuideVariantFitEditors = false;
            return;
        }

        GuideVariantFitCaptionTextBlock.Text = $"Selected variant: {selected.VariantName}";
        var scale = selected.ImageScale <= 0 ? 1 : selected.ImageScale;
        GuideVariantScaleTextBox.Text = scale.ToString("0.###", CultureInfo.InvariantCulture);
        var localRotation = double.IsFinite(selected.ImageLocalAlignmentRotationDegrees) ? selected.ImageLocalAlignmentRotationDegrees : 0;
        GuideVariantRotationTextBox.Text = localRotation.ToString("0.###", CultureInfo.InvariantCulture);
        var localOffsetX = double.IsFinite(selected.ImageLocalAlignmentOffsetX) ? selected.ImageLocalAlignmentOffsetX : 0;
        var localOffsetY = double.IsFinite(selected.ImageLocalAlignmentOffsetY) ? selected.ImageLocalAlignmentOffsetY : 0;
        GuideVariantOffsetXTextBox.Text = localOffsetX.ToString("0.###", CultureInfo.InvariantCulture);
        GuideVariantOffsetYTextBox.Text = localOffsetY.ToString("0.###", CultureInfo.InvariantCulture);

        _isUpdatingGuideVariantFitEditors = false;
    }

    private void GuideVariantScaleTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingGuideVariantFitEditors)
        {
            return;
        }

        if (GetSelectedVariant() is not { } selected)
        {
            return;
        }

        if (!TryParseStrictDouble(GuideVariantScaleTextBox.Text, out var parsedScale)
            || !double.IsFinite(parsedScale)
            || parsedScale <= 0)
        {
            return;
        }

        if (Math.Abs(selected.ImageScale - parsedScale) < 0.0001)
        {
            return;
        }

        selected.ImageScale = parsedScale;
        RefreshGuidePreview();
    }

    private void DrawFootprintGuide()
    {
        if (FootprintGuideGrid is null || GuideFootprintOverlay is null)
        {
            return;
        }

        FootprintGuideGrid.Children.Clear();
        ResolveGuideFootprintRect(out var overlayLeft, out var overlayTop, out var overlayWidth, out var overlayHeight);

        GuideFootprintOverlay.Width = Math.Max(0, overlayWidth);
        GuideFootprintOverlay.Height = Math.Max(0, overlayHeight);
        GuideFootprintOverlay.Margin = new Thickness(overlayLeft, overlayTop, 0, 0);

        for (var row = 0; row < GuideGridCellCount; row++)
        {
            for (var column = 0; column < GuideGridCellCount; column++)
            {
                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                    BorderThickness = new Thickness(0.5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)),
                    Margin = new Thickness(0.5)
                };

                FootprintGuideGrid.Children.Add(border);
            }
        }
    }

    private void ResolveGuideFootprintRect(out double overlayLeft, out double overlayTop, out double overlayWidth, out double overlayHeight)
    {
        var width = ParsePositiveIntOrFallback(FootprintWidthTextBox.Text, _footprintWidthCells);
        var height = ParsePositiveIntOrFallback(FootprintHeightTextBox.Text, _footprintHeightCells);
        var orientation = NormalizeCardinalDirection(GetSelectedComboString(FootprintOrientationComboBox, _footprintOrientation));
        GetOrientedFootprintDimensions(width, height, orientation, out var orientedWidth, out var orientedHeight);
        var guideSize = FootprintGuideGrid.ActualWidth > 0 ? FootprintGuideGrid.ActualWidth : GuideSurfaceSizePixels;
        var cellSize = guideSize / GuideGridCellCount;
        overlayWidth = orientedWidth * cellSize;
        overlayHeight = orientedHeight * cellSize;
        overlayLeft = (guideSize - overlayWidth) / 2d;
        overlayTop = (guideSize - overlayHeight) / 2d;
    }

    private void UpdateGuideLegend()
    {
        var normalizedOrientation = NormalizeCardinalDirection(GetSelectedComboString(FootprintOrientationComboBox, _footprintOrientation));
        var normalizedHeading = NormalizeHeadingDirection(GetSelectedComboString(HeadingDirectionComboBox, _headingDirection));
        GuideLegendTextBlock.Text = $"Blue box = footprint bounds. Gray grid = visual reference. Red arrow = object front (heading {normalizedHeading}). Footprint orientation = {normalizedOrientation}. Local rotation aligns the selected image variant to the footprint.";
    }

    private static void GetOrientedFootprintDimensions(
        int width,
        int height,
        string orientation,
        out int orientedWidth,
        out int orientedHeight)
    {
        var normalizedOrientation = NormalizeCardinalDirection(orientation);
        if (normalizedOrientation is "E" or "W")
        {
            orientedWidth = height;
            orientedHeight = width;
            return;
        }

        orientedWidth = width;
        orientedHeight = height;
    }

    private void UpdateGuideImageOverlay()
    {
        var selected = GetSelectedVariant();
        var source = selected is null
            ? null
            : LoadThumbnail(selected.FullImagePath, _projectFilePath, _preferredImageSourceBucket);
        GuideImageOverlay.Source = source;
        var baseWidth = ResolveGuideImageBaseWidthPixels(source);
        var baseHeight = ResolveGuideImageBaseHeightPixels(source);

        var scale = selected is null || selected.ImageScale <= 0
            ? 1
            : selected.ImageScale;
        GuideImageOverlay.Width = baseWidth * scale;
        GuideImageOverlay.Height = baseHeight * scale;
        var localRotation = selected is null || !double.IsFinite(selected.ImageLocalAlignmentRotationDegrees)
            ? 0
            : selected.ImageLocalAlignmentRotationDegrees;
        var localOffsetX = selected is null || !double.IsFinite(selected.ImageLocalAlignmentOffsetX)
            ? 0
            : selected.ImageLocalAlignmentOffsetX;
        var localOffsetY = selected is null || !double.IsFinite(selected.ImageLocalAlignmentOffsetY)
            ? 0
            : selected.ImageLocalAlignmentOffsetY;

        // Contract-aligned preview: anchor at footprint top-left, then add rotated local nudge.
        ResolveGuideFootprintRect(out var anchorX, out var anchorY, out _, out _);
        var rotatedNudge = RotateOffset(localOffsetX, localOffsetY, localRotation);
        GuideImageOverlay.Margin = new Thickness(anchorX + rotatedNudge.X, anchorY + rotatedNudge.Y, 0, 0);
        GuideImageOverlay.RenderTransform = new RotateTransform(localRotation);
    }

    private static (double X, double Y) RotateOffset(double offsetX, double offsetY, double rotationDegrees)
    {
        if (!double.IsFinite(offsetX) || !double.IsFinite(offsetY))
        {
            return (0, 0);
        }

        var radians = NormalizeDegrees(rotationDegrees) * (Math.PI / 180d);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return (
            X: (offsetX * cos) - (offsetY * sin),
            Y: (offsetX * sin) + (offsetY * cos));
    }

    private static double NormalizeDegrees(double degrees)
    {
        if (!double.IsFinite(degrees))
        {
            return 0;
        }

        var normalized = degrees % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }

    private double ResolveGuideImageBaseWidthPixels(BitmapSource? source)
    {
        if (source is null || source.PixelWidth <= 0)
        {
            return _projectRoomGridCellSize;
        }

        return source.PixelWidth;
    }

    private double ResolveGuideImageBaseHeightPixels(BitmapSource? source)
    {
        if (source is null || source.PixelHeight <= 0)
        {
            return _projectRoomGridCellSize;
        }

        return source.PixelHeight;
    }

    private void UpdateHeadingArrowTransform()
    {
        var headingDegrees = DirectionToDegrees(_headingDirection);
        GuideHeadingArrow.RenderTransform = new RotateTransform(headingDegrees);
    }

    private void ApplyVariantFitButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyGuideVariantFit(showValidationErrors: true);
    }

    private void DecreaseVariantScaleButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantScale(-VariantScaleNudgeStep);
    }

    private void IncreaseVariantScaleButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantScale(VariantScaleNudgeStep);
    }

    private void DecreaseVariantRotationButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantRotation(-15d);
    }

    private void IncreaseVariantRotationButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantRotation(15d);
    }

    private void DecreaseVariantOffsetXButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantOffsetX(-1d);
    }

    private void IncreaseVariantOffsetXButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantOffsetX(1d);
    }

    private void DecreaseVariantOffsetYButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantOffsetY(-1d);
    }

    private void IncreaseVariantOffsetYButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeVariantOffsetY(1d);
    }

    private void GuideVariantFitTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not Key.Enter)
        {
            return;
        }

        if (ApplyGuideVariantFit(showValidationErrors: true))
        {
            e.Handled = true;
        }
    }

    private bool ApplyGuideVariantFit(bool showValidationErrors)
    {
        if (GetSelectedVariant() is not { } selected)
        {
            return false;
        }

        if (!TryParseStrictDouble(GuideVariantScaleTextBox.Text, out var parsedScale)
            || !double.IsFinite(parsedScale)
            || parsedScale <= 0)
        {
            if (showValidationErrors)
            {
                System.Windows.MessageBox.Show(this, "Scale must be a positive decimal number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
                GuideVariantScaleTextBox.Focus();
                GuideVariantScaleTextBox.SelectAll();
            }

            return false;
        }

        if (!TryParseStrictDouble(GuideVariantRotationTextBox.Text, out var parsedRotation)
            || !double.IsFinite(parsedRotation))
        {
            if (showValidationErrors)
            {
                System.Windows.MessageBox.Show(this, "Local rotation must be a valid decimal number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
                GuideVariantRotationTextBox.Focus();
                GuideVariantRotationTextBox.SelectAll();
            }

            return false;
        }

        if (!TryParseStrictDouble(GuideVariantOffsetXTextBox.Text, out var parsedOffsetX)
            || !double.IsFinite(parsedOffsetX))
        {
            if (showValidationErrors)
            {
                System.Windows.MessageBox.Show(this, "Local offset X must be a valid decimal number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
                GuideVariantOffsetXTextBox.Focus();
                GuideVariantOffsetXTextBox.SelectAll();
            }

            return false;
        }

        if (!TryParseStrictDouble(GuideVariantOffsetYTextBox.Text, out var parsedOffsetY)
            || !double.IsFinite(parsedOffsetY))
        {
            if (showValidationErrors)
            {
                System.Windows.MessageBox.Show(this, "Local offset Y must be a valid decimal number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
                GuideVariantOffsetYTextBox.Focus();
                GuideVariantOffsetYTextBox.SelectAll();
            }

            return false;
        }

        selected.ImageScale = parsedScale;
        selected.ImageLocalAlignmentRotationDegrees = parsedRotation;
        selected.ImageLocalAlignmentOffsetX = parsedOffsetX;
        selected.ImageLocalAlignmentOffsetY = parsedOffsetY;
        RefreshGuidePreview();
        return true;
    }

    private void NudgeVariantScale(double delta)
    {
        var current = ParseDoubleOrFallback(GuideVariantScaleTextBox.Text, GetSelectedVariant()?.ImageScale is > 0 ? GetSelectedVariant()!.ImageScale : 1d);
        var nudged = Math.Max(VariantScaleNudgeMinimum, Math.Round(current + delta, 3, MidpointRounding.AwayFromZero));
        GuideVariantScaleTextBox.Text = nudged.ToString("0.###", CultureInfo.InvariantCulture);
        ApplyGuideVariantFit(showValidationErrors: false);
    }

    private void NudgeVariantRotation(double delta)
    {
        var fallback = GetSelectedVariant() is { } selected && double.IsFinite(selected.ImageLocalAlignmentRotationDegrees)
            ? selected.ImageLocalAlignmentRotationDegrees
            : 0d;
        var current = ParseDoubleOrFallback(GuideVariantRotationTextBox.Text, fallback);
        var nudged = Math.Round(current + delta, 3, MidpointRounding.AwayFromZero);
        GuideVariantRotationTextBox.Text = nudged.ToString("0.###", CultureInfo.InvariantCulture);
        ApplyGuideVariantFit(showValidationErrors: false);
    }

    private void NudgeVariantOffsetX(double delta)
    {
        var fallback = GetSelectedVariant() is { } selected && double.IsFinite(selected.ImageLocalAlignmentOffsetX)
            ? selected.ImageLocalAlignmentOffsetX
            : 0d;
        var current = ParseDoubleOrFallback(GuideVariantOffsetXTextBox.Text, fallback);
        var nudged = Math.Round(current + delta, 3, MidpointRounding.AwayFromZero);
        GuideVariantOffsetXTextBox.Text = nudged.ToString("0.###", CultureInfo.InvariantCulture);
        ApplyGuideVariantFit(showValidationErrors: false);
    }

    private void NudgeVariantOffsetY(double delta)
    {
        var fallback = GetSelectedVariant() is { } selected && double.IsFinite(selected.ImageLocalAlignmentOffsetY)
            ? selected.ImageLocalAlignmentOffsetY
            : 0d;
        var current = ParseDoubleOrFallback(GuideVariantOffsetYTextBox.Text, fallback);
        var nudged = Math.Round(current + delta, 3, MidpointRounding.AwayFromZero);
        GuideVariantOffsetYTextBox.Text = nudged.ToString("0.###", CultureInfo.InvariantCulture);
        ApplyGuideVariantFit(showValidationErrors: false);
    }

    private void EnsureSingleDefault()
    {
        var defaultIndex = _imageVariants.FindIndex(static variant => variant.IsDefault);
        if (defaultIndex < 0)
        {
            _imageVariants[0].IsDefault = true;
            return;
        }

        for (var index = 0; index < _imageVariants.Count; index++)
        {
            _imageVariants[index].IsDefault = index == defaultIndex;
        }
    }

    private string GenerateUniqueVariantName()
    {
        var next = 1;
        while (_imageVariants.Any(variant => string.Equals(variant.VariantName, $"variant{next}", StringComparison.OrdinalIgnoreCase)))
        {
            next++;
        }

        return $"variant{next}";
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        _isMovable = IsMovableCheckBox.IsChecked == true;
        _isMovableDefaultValue = GetSelectedBoolean(MovableDefaultValueComboBox);
        _spatialType = ParseSpatialType(GetSelectedComboString(SpatialTypeComboBox, nameof(RuntimeObjectSpatialTypes.SolidObject)));

        if (!int.TryParse(StackOrderTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStackOrder)
            || parsedStackOrder < 0)
        {
            System.Windows.MessageBox.Show(this, "Stack group must be a number greater than or equal to 0.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            StackOrderTextBox.Focus();
            StackOrderTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(FootprintWidthTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFootprintWidth)
            || parsedFootprintWidth < 1)
        {
            System.Windows.MessageBox.Show(this, "Footprint width must be a number greater than or equal to 1.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            FootprintWidthTextBox.Focus();
            FootprintWidthTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(FootprintHeightTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFootprintHeight)
            || parsedFootprintHeight < 1)
        {
            System.Windows.MessageBox.Show(this, "Footprint height must be a number greater than or equal to 1.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            FootprintHeightTextBox.Focus();
            FootprintHeightTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(ObjectHeightUnitsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHeightUnits)
            || parsedHeightUnits < 0)
        {
            System.Windows.MessageBox.Show(this, "Object height units must be a number greater than or equal to 0.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            ObjectHeightUnitsTextBox.Focus();
            ObjectHeightUnitsTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(HeightInRoomTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHeightInRoom)
            || parsedHeightInRoom < 0)
        {
            System.Windows.MessageBox.Show(this, "Height in room must be a number greater than or equal to 0.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            HeightInRoomTextBox.Focus();
            HeightInRoomTextBox.SelectAll();
            return;
        }

        if (!TryParseOptionalDouble(StackScaleStepOverrideTextBox.Text, out var parsedStackScaleStep))
        {
            System.Windows.MessageBox.Show(this, "Stack scale step must be blank or a valid decimal number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            StackScaleStepOverrideTextBox.Focus();
            StackScaleStepOverrideTextBox.SelectAll();
            return;
        }

        if (parsedStackScaleStep.HasValue && !StackScalePolicy.IsStackScaleStepWithinBounds(parsedStackScaleStep.Value))
        {
            System.Windows.MessageBox.Show(
                this,
                $"Stack scale step override must be blank or between {StackScalePolicy.MinStackScaleStep:0.##} and {StackScalePolicy.MaxStackScaleStep:0.##}.",
                "Object Appearance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            StackScaleStepOverrideTextBox.Focus();
            StackScaleStepOverrideTextBox.SelectAll();
            return;
        }

        if (!TryParseOptionalDouble(MinStackScaleOverrideTextBox.Text, out var parsedMinStackScale))
        {
            System.Windows.MessageBox.Show(this, "Min stack scale must be blank or a valid decimal number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            MinStackScaleOverrideTextBox.Focus();
            MinStackScaleOverrideTextBox.SelectAll();
            return;
        }

        if (parsedMinStackScale.HasValue && !StackScalePolicy.IsMinStackScaleWithinBounds(parsedMinStackScale.Value))
        {
            System.Windows.MessageBox.Show(
                this,
                $"Min stack scale override must be blank or between {StackScalePolicy.MinMinStackScale:0.##} and {StackScalePolicy.MaxMinStackScale:0.##}.",
                "Object Appearance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            MinStackScaleOverrideTextBox.Focus();
            MinStackScaleOverrideTextBox.SelectAll();
            return;
        }

        if (_spatialType == RuntimeObjectSpatialTypes.PassiveObject)
        {
            parsedStackOrder = 0;
        }

        _stackOrder = parsedStackOrder;
        _footprintWidthCells = parsedFootprintWidth;
        _footprintHeightCells = parsedFootprintHeight;
        _footprintOrientation = NormalizeCardinalDirection(GetSelectedComboString(FootprintOrientationComboBox, "N"));
        _headingDirection = NormalizeHeadingDirection(GetSelectedComboString(HeadingDirectionComboBox, "N"));
        _objectHeightUnits = parsedHeightUnits;
        _heightInRoom = parsedHeightInRoom;
        _stackScaleStepOverride = SanitizeOptionalStackScaleStep(parsedStackScaleStep);
        _minStackScaleOverride = SanitizeOptionalMinStackScale(parsedMinStackScale);

        EnsureSingleDefault();
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static void SetBooleanComboSelection(System.Windows.Controls.ComboBox comboBox, bool value)
    {
        comboBox.SelectedIndex = value ? 1 : 0;
    }

    private static bool GetSelectedBoolean(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item)
        {
            return false;
        }

        return string.Equals(item.Content?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSelectedComboString(System.Windows.Controls.ComboBox comboBox, string fallback)
    {
        if (comboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item)
        {
            return fallback;
        }

        var value = item.Content?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static void SetStringComboSelection(System.Windows.Controls.ComboBox comboBox, string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var item in comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static bool TryParseOptionalDouble(string text, out double? parsed)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            parsed = null;
            return true;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value))
        {
            parsed = value;
            return true;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value))
        {
            parsed = value;
            return true;
        }

        parsed = null;
        return false;
    }

    private static bool TryParseStrictDouble(string text, out double parsed)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            parsed = 0;
            return false;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
            && double.IsFinite(parsed))
        {
            return true;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)
            && double.IsFinite(parsed))
        {
            return true;
        }

        parsed = 0;
        return false;
    }

    private static string FormatOptionalDouble(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string NormalizeCardinalDirection(string? direction)
    {
        var value = direction?.Trim().ToUpperInvariant();
        return value is "N" or "E" or "S" or "W" ? value : "N";
    }

    private static string NormalizeHeadingDirection(string? direction)
    {
        var value = direction?.Trim().ToUpperInvariant();
        return value is "N" or "NE" or "E" or "SE" or "S" or "SW" or "W" or "NW" ? value : "N";
    }

    private static RuntimeObjectSpatialTypes ParseSpatialType(string? value)
    {
        if (string.Equals(value?.Trim(), nameof(RuntimeObjectSpatialTypes.PassiveObject), StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeObjectSpatialTypes.PassiveObject;
        }

        return RuntimeObjectSpatialTypes.SolidObject;
    }

    private static double DirectionToDegrees(string? direction)
    {
        return NormalizeHeadingDirection(direction) switch
        {
            "N" => 0,
            "NE" => 45,
            "E" => 90,
            "SE" => 135,
            "S" => 180,
            "SW" => 225,
            "W" => 270,
            "NW" => 315,
            _ => 0
        };
    }

    private static double? SanitizeOptionalFinite(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value)
            ? value
            : null;
    }

    private static double? SanitizeOptionalStackScaleStep(double? value)
    {
        var finite = SanitizeOptionalFinite(value);
        return finite.HasValue ? StackScalePolicy.ClampStackScaleStep(finite.Value) : null;
    }

    private static double? SanitizeOptionalMinStackScale(double? value)
    {
        var finite = SanitizeOptionalFinite(value);
        return finite.HasValue ? StackScalePolicy.ClampMinStackScale(finite.Value) : null;
    }

    private void WholeNumberTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void WholeNumberTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(System.Windows.DataFormats.Text) as string;
        if (string.IsNullOrWhiteSpace(pastedText) || !pastedText.All(char.IsDigit))
        {
            e.CancelCommand();
        }
    }

    private void FootprintDimensionTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        DrawFootprintGuide();
    }

    private void OptionalDecimalTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 0)
        {
            e.Handled = false;
            return;
        }

        e.Handled = !e.Text.All(static c => char.IsDigit(c) || c == '.' || c == ',');
    }

    private void OptionalDecimalTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(System.Windows.DataFormats.Text) as string;
        if (pastedText is null)
        {
            e.CancelCommand();
            return;
        }

        var normalized = pastedText.Trim();
        if (normalized.Length == 0)
        {
            return;
        }

        if (!TryParseOptionalDouble(normalized, out _))
        {
            e.CancelCommand();
        }
    }

    private static int ParsePositiveIntOrFallback(string? value, int fallback)
    {
        if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < 1)
        {
            return fallback < 1 ? 1 : fallback;
        }

        return parsed;
    }

    private static double ParseDoubleOrFallback(string? value, double fallback)
    {
        return TryParseStrictDouble(value ?? string.Empty, out var parsed)
            ? parsed
            : fallback;
    }
}
