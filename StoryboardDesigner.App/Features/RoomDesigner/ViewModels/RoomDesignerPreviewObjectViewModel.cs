using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Storyboard.Shared.GameServices.Spatial;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public enum RoomDesignerPreviewPlacementComparisonMode
{
    SharedOnly
}

public sealed class RoomDesignerPreviewObjectViewModel : ViewModelBase
{
    private readonly Action? _onEdited;
    private readonly Func<string?>? _projectFilePathAccessor;
    private readonly Func<Guid, GameObject?>? _definitionResolver;
    private bool _isSelected;
    private int _drawOrderZIndex;
    private string? _selectedPreviewVariantName;
    private int _gridCellSize = 40;
    private RoomDesignerPreviewPlacementComparisonMode _placementComparisonMode = RoomDesignerPreviewPlacementComparisonMode.SharedOnly;
    private GameObject _effectiveDefinitionOwnedSource;

    public RoomDesignerPreviewObjectViewModel(
        GameObject gameObject,
        Action? onEdited = null,
        Func<string?>? projectFilePathAccessor = null,
        Func<Guid, GameObject?>? definitionResolver = null)
    {
        GameObject = gameObject;
        _onEdited = onEdited;
        _projectFilePathAccessor = projectFilePathAccessor;
        _definitionResolver = definitionResolver;
        _effectiveDefinitionOwnedSource = gameObject;
        SelectPreviewVariantCommand = new RelayCommandOfT<string>(SelectPreviewVariant);
        RefreshEffectiveDefinitionOwnedSource();
        GameObject.PropertyChanged += OnGameObjectPropertyChanged;
    }

    public GameObject GameObject { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(GameObject.Name)
        ? "(unnamed object)"
        : GameObject.Name;

    public ICommand SelectPreviewVariantCommand { get; }

    public IReadOnlyList<string> AvailablePreviewVariantNames => _effectiveDefinitionOwnedSource.ImageVariants
        .Where(static variant => !string.IsNullOrWhiteSpace(variant.VariantName))
        .Select(static variant => variant.VariantName.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public bool HasMultiplePreviewVariants => AvailablePreviewVariantNames.Count > 1;

    public string? SelectedPreviewVariantName
    {
        get => _selectedPreviewVariantName;
        set
        {
            if (string.Equals(_selectedPreviewVariantName, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedPreviewVariantName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThumbnailSource));
            OnPropertyChanged(nameof(PreviewBaseIconWidthPixels));
            OnPropertyChanged(nameof(PreviewBaseIconHeightPixels));
            OnPropertyChanged(nameof(PreviewRenderedIconWidthPixels));
            OnPropertyChanged(nameof(PreviewRenderedIconHeightPixels));
            OnPropertyChanged(nameof(PreviewBaseIconSizePixels));
            OnPropertyChanged(nameof(ImageRotationDegrees));
            OnPropertyChanged(nameof(ImageLocalAlignmentOffsetX));
            OnPropertyChanged(nameof(ImageLocalAlignmentOffsetY));
            OnPropertyChanged(nameof(ImageScale));
            OnPropertyChanged(nameof(EffectiveImageScale));
            OnPropertyChanged(nameof(IconLayerOffsetXPixels));
            OnPropertyChanged(nameof(IconLayerOffsetYPixels));
            OnPropertyChanged(nameof(IconLayerPositionX));
            OnPropertyChanged(nameof(IconLayerPositionY));
            OnPropertyChanged(nameof(IsRenderable));
        }
    }

    public bool IncludeInPreview
    {
        get => GameObject.IncludeInPreview;
        set
        {
            if (GameObject.IncludeInPreview == value)
            {
                return;
            }

            GameObject.IncludeInPreview = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRenderable));
            _onEdited?.Invoke();
        }
    }

    public double PositionX
    {
        get => GameObject.PositionX;
        set
        {
            if (Math.Abs(GameObject.PositionX - value) < 0.0001)
            {
                return;
            }

            GameObject.PositionX = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public double PositionY
    {
        get => GameObject.PositionY;
        set
        {
            if (Math.Abs(GameObject.PositionY - value) < 0.0001)
            {
                return;
            }

            GameObject.PositionY = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public double RoomRotationDegrees
    {
        get => GameObject.ImageRotationDegrees;
        set
        {
            var sanitized = double.IsFinite(value) ? value : 0;
            var snapped = SnapRoomRotationToQuarterTurn(sanitized);
            if (Math.Abs(GameObject.ImageRotationDegrees - snapped) < 0.0001)
            {
                return;
            }

            GameObject.ImageRotationDegrees = snapped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImageRotationDegrees));
            _onEdited?.Invoke();
        }
    }

    // Final image angle shown in room preview: room-authored rotation + variant-local fit rotation.
    public double ImageRotationDegrees => GameObject.ImageRotationDegrees + _effectiveDefinitionOwnedSource.ResolveImageLocalAlignmentRotationDegrees(SelectedPreviewVariantName);

    // Local icon placement inside the footprint frame after applying preview transform rules.
    public double ImageLocalAlignmentOffsetX => ResolveRotatedImageLocalAlignmentOffset().X;

    // Local icon placement inside the footprint frame after applying preview transform rules.
    public double ImageLocalAlignmentOffsetY => ResolveRotatedImageLocalAlignmentOffset().Y;

    public RoomDesignerPreviewPlacementComparisonMode PlacementComparisonMode
    {
        get => _placementComparisonMode;
        set
        {
            if (_placementComparisonMode == value)
            {
                return;
            }

            _placementComparisonMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImageLocalAlignmentOffsetX));
            OnPropertyChanged(nameof(ImageLocalAlignmentOffsetY));
            OnPropertyChanged(nameof(PlacementComparisonDeltaX));
            OnPropertyChanged(nameof(PlacementComparisonDeltaY));
            OnPropertyChanged(nameof(PlacementComparisonSnapshot));
        }
    }

    // Shared - legacy offset delta, useful while running side-by-side comparisons.
    public double PlacementComparisonDeltaX => ResolvePlacementComparisonDelta().X;

    // Shared - legacy offset delta, useful while running side-by-side comparisons.
    public double PlacementComparisonDeltaY => ResolvePlacementComparisonDelta().Y;

    // Diagnostics snapshot for side-by-side parity investigation without changing selected draw behavior.
    public string PlacementComparisonSnapshot => BuildPlacementComparisonSnapshot();

    public double ImageScale => _effectiveDefinitionOwnedSource.ResolveImageScale(SelectedPreviewVariantName);

    public double EffectiveImageScale => ImageScale > 0 ? ImageScale : 1d;

    public double PreviewBaseIconWidthPixels => ResolvePreviewBaseIconWidthPixels();

    public double PreviewBaseIconHeightPixels => ResolvePreviewBaseIconHeightPixels();

    public double PreviewRenderedIconWidthPixels => PreviewBaseIconWidthPixels * EffectiveImageScale;

    public double PreviewRenderedIconHeightPixels => PreviewBaseIconHeightPixels * EffectiveImageScale;

    public double PreviewBaseIconSizePixels => Math.Max(PreviewBaseIconWidthPixels, PreviewBaseIconHeightPixels);

    public double PreviewBaseIconOffsetXPixels => 0;

    public double PreviewBaseIconOffsetYPixels => 0;

    public Thickness PreviewBaseIconMargin => new(PreviewBaseIconOffsetXPixels, PreviewBaseIconOffsetYPixels, 0, 0);

    public double IconLayerOffsetXPixels => PreviewBaseIconOffsetXPixels;

    public double IconLayerOffsetYPixels => PreviewBaseIconOffsetYPixels;

    public double IconLayerPositionX => PositionX + IconLayerOffsetXPixels;

    public double IconLayerPositionY => PositionY + IconLayerOffsetYPixels;

    // Footprint dimensions are axis-swapped when orientation/rotation is an odd quarter-turn.
    public int OrientedFootprintWidthCells => UsesSwappedFootprintAxes(GameObject.FootprintOrientation, GameObject.ImageRotationDegrees)
        ? ResolveFootprintCellCount(GameObject.FootprintHeightCells)
        : ResolveFootprintCellCount(GameObject.FootprintWidthCells);

    // Footprint dimensions are axis-swapped when orientation/rotation is an odd quarter-turn.
    public int OrientedFootprintHeightCells => UsesSwappedFootprintAxes(GameObject.FootprintOrientation, GameObject.ImageRotationDegrees)
        ? ResolveFootprintCellCount(GameObject.FootprintWidthCells)
        : ResolveFootprintCellCount(GameObject.FootprintHeightCells);

    public double FootprintWidthPixels => OrientedFootprintWidthCells * Math.Max(1, _gridCellSize);

    public double FootprintHeightPixels => OrientedFootprintHeightCells * Math.Max(1, _gridCellSize);

    public int FootprintLayerZIndex => _drawOrderZIndex * 2;

    public int IconLayerZIndex => (_drawOrderZIndex * 2) + 1;

    public int DrawOrderZIndex
    {
        get => _drawOrderZIndex;
        set
        {
            if (_drawOrderZIndex == value)
            {
                return;
            }

            _drawOrderZIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FootprintLayerZIndex));
            OnPropertyChanged(nameof(IconLayerZIndex));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsRenderable => IncludeInPreview && ThumbnailSource is not null;

    public BitmapImage? ThumbnailSource => LoadThumbnail(_effectiveDefinitionOwnedSource.ResolveImagePath(SelectedPreviewVariantName));

    public void UpdateGridCellSize(int gridCellSize)
    {
        var sanitized = gridCellSize > 0 ? gridCellSize : 40;
        if (_gridCellSize == sanitized)
        {
            return;
        }

        _gridCellSize = sanitized;
        OnPropertyChanged(nameof(FootprintWidthPixels));
        OnPropertyChanged(nameof(FootprintHeightPixels));
        OnPropertyChanged(nameof(ImageLocalAlignmentOffsetX));
        OnPropertyChanged(nameof(ImageLocalAlignmentOffsetY));
        OnPropertyChanged(nameof(PlacementComparisonDeltaX));
        OnPropertyChanged(nameof(PlacementComparisonDeltaY));
        OnPropertyChanged(nameof(PlacementComparisonSnapshot));
        OnPropertyChanged(nameof(PreviewBaseIconWidthPixels));
        OnPropertyChanged(nameof(PreviewBaseIconHeightPixels));
        OnPropertyChanged(nameof(PreviewRenderedIconWidthPixels));
        OnPropertyChanged(nameof(PreviewRenderedIconHeightPixels));
        OnPropertyChanged(nameof(PreviewBaseIconSizePixels));
        OnPropertyChanged(nameof(PreviewBaseIconOffsetXPixels));
        OnPropertyChanged(nameof(PreviewBaseIconOffsetYPixels));
        OnPropertyChanged(nameof(PreviewBaseIconMargin));
        OnPropertyChanged(nameof(IconLayerOffsetXPixels));
        OnPropertyChanged(nameof(IconLayerOffsetYPixels));
        OnPropertyChanged(nameof(IconLayerPositionX));
        OnPropertyChanged(nameof(IconLayerPositionY));
    }

    private void SelectPreviewVariant(string? variantName)
    {
        SelectedPreviewVariantName = variantName;
    }

    private BitmapImage? LoadThumbnail(string configuredPath)
    {
        var resolvedPath = DesignerImagePathResolver.ResolveForPreview(configuredPath, _projectFilePathAccessor?.Invoke(), preferredSourceBucket: "objects");
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

    private void OnGameObjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GameObject.Name):
                OnPropertyChanged(nameof(DisplayName));
                break;
            case nameof(GameObject.LinkedBaseObjectId):
                RefreshEffectiveDefinitionOwnedSource(raiseDependentProperties: true);
                break;
            case nameof(GameObject.ImageVariants):
                RaiseImageDerivedPropertyChanges();
                break;
            case nameof(GameObject.FootprintWidthCells):
            case nameof(GameObject.FootprintHeightCells):
            case nameof(GameObject.FootprintOrientation):
                OnPropertyChanged(nameof(OrientedFootprintWidthCells));
                OnPropertyChanged(nameof(OrientedFootprintHeightCells));
                OnPropertyChanged(nameof(FootprintWidthPixels));
                OnPropertyChanged(nameof(FootprintHeightPixels));
                OnPropertyChanged(nameof(ImageLocalAlignmentOffsetX));
                OnPropertyChanged(nameof(ImageLocalAlignmentOffsetY));
                OnPropertyChanged(nameof(PlacementComparisonDeltaX));
                OnPropertyChanged(nameof(PlacementComparisonDeltaY));
                OnPropertyChanged(nameof(PlacementComparisonSnapshot));
                OnPropertyChanged(nameof(PreviewBaseIconWidthPixels));
                OnPropertyChanged(nameof(PreviewBaseIconHeightPixels));
                OnPropertyChanged(nameof(PreviewRenderedIconWidthPixels));
                OnPropertyChanged(nameof(PreviewRenderedIconHeightPixels));
                OnPropertyChanged(nameof(PreviewBaseIconSizePixels));
                OnPropertyChanged(nameof(PreviewBaseIconOffsetXPixels));
                OnPropertyChanged(nameof(PreviewBaseIconOffsetYPixels));
                OnPropertyChanged(nameof(PreviewBaseIconMargin));
                OnPropertyChanged(nameof(IconLayerOffsetXPixels));
                OnPropertyChanged(nameof(IconLayerOffsetYPixels));
                OnPropertyChanged(nameof(IconLayerPositionX));
                OnPropertyChanged(nameof(IconLayerPositionY));
                break;
            case nameof(GameObject.ImageRotationDegrees):
                OnPropertyChanged(nameof(RoomRotationDegrees));
                OnPropertyChanged(nameof(ImageRotationDegrees));
                OnPropertyChanged(nameof(OrientedFootprintWidthCells));
                OnPropertyChanged(nameof(OrientedFootprintHeightCells));
                OnPropertyChanged(nameof(FootprintWidthPixels));
                OnPropertyChanged(nameof(FootprintHeightPixels));
                OnPropertyChanged(nameof(ImageLocalAlignmentOffsetX));
                OnPropertyChanged(nameof(ImageLocalAlignmentOffsetY));
                OnPropertyChanged(nameof(PlacementComparisonDeltaX));
                OnPropertyChanged(nameof(PlacementComparisonDeltaY));
                OnPropertyChanged(nameof(PlacementComparisonSnapshot));
                OnPropertyChanged(nameof(PreviewBaseIconWidthPixels));
                OnPropertyChanged(nameof(PreviewBaseIconHeightPixels));
                OnPropertyChanged(nameof(PreviewRenderedIconWidthPixels));
                OnPropertyChanged(nameof(PreviewRenderedIconHeightPixels));
                OnPropertyChanged(nameof(PreviewBaseIconSizePixels));
                OnPropertyChanged(nameof(PreviewBaseIconOffsetXPixels));
                OnPropertyChanged(nameof(PreviewBaseIconOffsetYPixels));
                OnPropertyChanged(nameof(PreviewBaseIconMargin));
                OnPropertyChanged(nameof(IconLayerOffsetXPixels));
                OnPropertyChanged(nameof(IconLayerOffsetYPixels));
                OnPropertyChanged(nameof(IconLayerPositionX));
                OnPropertyChanged(nameof(IconLayerPositionY));
                break;
            case nameof(GameObject.IncludeInPreview):
                OnPropertyChanged(nameof(IncludeInPreview));
                OnPropertyChanged(nameof(IsRenderable));
                break;
            case nameof(GameObject.PositionX):
                OnPropertyChanged(nameof(PositionX));
                OnPropertyChanged(nameof(IconLayerPositionX));
                break;
            case nameof(GameObject.PositionY):
                OnPropertyChanged(nameof(PositionY));
                OnPropertyChanged(nameof(IconLayerPositionY));
                break;
        }
    }

    private void OnEffectiveDefinitionOwnedSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(GameObject.ImageVariants), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.ImageVariantChooserScript), StringComparison.Ordinal))
        {
            RaiseImageDerivedPropertyChanges();
        }
    }

    private static bool UsesSwappedFootprintAxes(string? orientation, double roomRotationDegrees)
    {
        var swappedByOrientation = string.Equals(orientation, "E", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(orientation, "W", StringComparison.OrdinalIgnoreCase);

        var swappedByRoomRotation = IsOddQuarterTurn(roomRotationDegrees);
        return swappedByOrientation ^ swappedByRoomRotation;
    }

    private (double X, double Y) ResolveRotatedImageLocalAlignmentOffset()
    {
        return ComputeSharedPlacementOffset();
    }

    private (double X, double Y) ResolvePlacementComparisonDelta()
    {
        return (0d, 0d);
    }

    private string BuildPlacementComparisonSnapshot()
    {
        var selected = ComputeSharedPlacementOffset();
        return
            $"mode={PlacementComparisonMode};selected=({selected.X:0.###},{selected.Y:0.###});" +
            $"legacy=({selected.X:0.###},{selected.Y:0.###});" +
            $"shared=({selected.X:0.###},{selected.Y:0.###});" +
            "delta=(0,0)";
    }

    private (double X, double Y) ComputeSharedPlacementOffset()
    {
        var rawOffsetX = _effectiveDefinitionOwnedSource.ResolveImageLocalAlignmentOffsetX(SelectedPreviewVariantName);
        var rawOffsetY = _effectiveDefinitionOwnedSource.ResolveImageLocalAlignmentOffsetY(SelectedPreviewVariantName);
        var localRotationDegrees = _effectiveDefinitionOwnedSource.ResolveImageLocalAlignmentRotationDegrees(SelectedPreviewVariantName);
        var roomRotationDegrees = GameObject.ImageRotationDegrees;

        return RuntimeRenderablePlacementMath.ComputeIconLocalOffset(
            roomRotationDegrees,
            localRotationDegrees,
            rawOffsetX,
            rawOffsetY,
            FootprintWidthPixels,
            FootprintHeightPixels,
            PreviewRenderedIconWidthPixels,
            PreviewRenderedIconHeightPixels);
    }

    private double ResolvePreviewBaseIconWidthPixels()
    {
        var thumbnail = ThumbnailSource;
        if (thumbnail is null || thumbnail.PixelWidth <= 0)
        {
            return Math.Max(1, _gridCellSize);
        }

        return thumbnail.PixelWidth;
    }

    private double ResolvePreviewBaseIconHeightPixels()
    {
        var thumbnail = ThumbnailSource;
        if (thumbnail is null || thumbnail.PixelHeight <= 0)
        {
            return Math.Max(1, _gridCellSize);
        }

        return thumbnail.PixelHeight;
    }

    private static int ResolveFootprintCellCount(int? configured)
    {
        var value = configured.GetValueOrDefault();
        return value > 0 ? value : 1;
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

    private static bool IsOddQuarterTurn(double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        var quarterTurns = (int)Math.Round(normalized / 90d);
        var snapped = quarterTurns * 90d;
        if (Math.Abs(normalized - snapped) > 0.0001)
        {
            return false;
        }

        return (quarterTurns & 1) == 1;
    }

    private static double SnapRoomRotationToQuarterTurn(double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        var quarterTurns = (int)Math.Round(normalized / 90d, MidpointRounding.AwayFromZero);
        var snapped = quarterTurns * 90d;
        snapped %= 360d;
        return snapped < 0 ? snapped + 360d : snapped;
    }

    private void RefreshEffectiveDefinitionOwnedSource(bool raiseDependentProperties = false)
    {
        var resolved = ResolveEffectiveDefinitionOwnedSource();
        if (ReferenceEquals(resolved, _effectiveDefinitionOwnedSource))
        {
            return;
        }

        if (!ReferenceEquals(_effectiveDefinitionOwnedSource, GameObject))
        {
            _effectiveDefinitionOwnedSource.PropertyChanged -= OnEffectiveDefinitionOwnedSourcePropertyChanged;
        }

        _effectiveDefinitionOwnedSource = resolved;

        if (!ReferenceEquals(_effectiveDefinitionOwnedSource, GameObject))
        {
            _effectiveDefinitionOwnedSource.PropertyChanged += OnEffectiveDefinitionOwnedSourcePropertyChanged;
        }

        if (raiseDependentProperties)
        {
            RaiseImageDerivedPropertyChanges();
        }
    }

    private GameObject ResolveEffectiveDefinitionOwnedSource()
    {
        if (_definitionResolver is null || !GameObject.LinkedBaseObjectId.HasValue)
        {
            return GameObject;
        }

        var definition = _definitionResolver(GameObject.LinkedBaseObjectId.Value);
        return definition is null || ReferenceEquals(definition, GameObject)
            ? GameObject
            : definition;
    }

    private void RaiseImageDerivedPropertyChanges()
    {
        if (!string.IsNullOrWhiteSpace(_selectedPreviewVariantName)
            && !AvailablePreviewVariantNames.Contains(_selectedPreviewVariantName, StringComparer.OrdinalIgnoreCase))
        {
            _selectedPreviewVariantName = null;
            OnPropertyChanged(nameof(SelectedPreviewVariantName));
        }

        OnPropertyChanged(nameof(AvailablePreviewVariantNames));
        OnPropertyChanged(nameof(HasMultiplePreviewVariants));
        OnPropertyChanged(nameof(ThumbnailSource));
        OnPropertyChanged(nameof(PreviewBaseIconWidthPixels));
        OnPropertyChanged(nameof(PreviewBaseIconHeightPixels));
        OnPropertyChanged(nameof(PreviewRenderedIconWidthPixels));
        OnPropertyChanged(nameof(PreviewRenderedIconHeightPixels));
        OnPropertyChanged(nameof(PreviewBaseIconSizePixels));
        OnPropertyChanged(nameof(ImageRotationDegrees));
        OnPropertyChanged(nameof(ImageLocalAlignmentOffsetX));
        OnPropertyChanged(nameof(ImageLocalAlignmentOffsetY));
        OnPropertyChanged(nameof(PlacementComparisonDeltaX));
        OnPropertyChanged(nameof(PlacementComparisonDeltaY));
        OnPropertyChanged(nameof(PlacementComparisonSnapshot));
        OnPropertyChanged(nameof(ImageScale));
        OnPropertyChanged(nameof(EffectiveImageScale));
        OnPropertyChanged(nameof(IconLayerOffsetXPixels));
        OnPropertyChanged(nameof(IconLayerOffsetYPixels));
        OnPropertyChanged(nameof(IconLayerPositionX));
        OnPropertyChanged(nameof(IconLayerPositionY));
        OnPropertyChanged(nameof(IsRenderable));
    }
}
