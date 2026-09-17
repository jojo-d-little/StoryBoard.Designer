using System.Windows.Media.Imaging;
using System.IO;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomDesignerImageSlotViewModel : ViewModelBase
{
    private readonly Action? _onEdited;
    private readonly Func<string?>? _projectFilePathAccessor;
    private readonly string? _preferredSourceBucket;
    private bool _isPreviewVisible = true;

    public RoomDesignerImageSlotViewModel(
        RoomImageEntry entry,
        Action? onEdited = null,
        Func<string?>? projectFilePathAccessor = null,
        string? preferredSourceBucket = null)
    {
        Entry = entry;
        _onEdited = onEdited;
        _projectFilePathAccessor = projectFilePathAccessor;
        _preferredSourceBucket = preferredSourceBucket;
    }

    public RoomImageEntry Entry { get; }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            if (_isPreviewVisible == value)
            {
                return;
            }

            _isPreviewVisible = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public bool HasConfiguredImage => !string.IsNullOrWhiteSpace(FullImagePath);

    public string DirectionShortLabel => Entry.Slot switch
    {
        RoomImageSlot.North => "N",
        RoomImageSlot.NorthEast => "NE",
        RoomImageSlot.East => "E",
        RoomImageSlot.SouthEast => "SE",
        RoomImageSlot.South => "S",
        RoomImageSlot.SouthWest => "SW",
        RoomImageSlot.West => "W",
        RoomImageSlot.NorthWest => "NW",
        _ => Entry.Slot.ToString()
    };

    public string DirectionStatusText => HasConfiguredImage ? "Configured" : "Not Configured";

    public string SlotLabel => Entry.Slot switch
    {
        RoomImageSlot.Default => "Default",
        RoomImageSlot.Up => "Look Up (Ceiling)",
        RoomImageSlot.Down => "Look Down (Floor)",
        _ => $"Looking {Entry.Slot}"
    };


    public System.Windows.HorizontalAlignment OverlayHorizontalAlignment => Entry.Slot switch
    {
        RoomImageSlot.North => System.Windows.HorizontalAlignment.Center,
        RoomImageSlot.NorthEast => System.Windows.HorizontalAlignment.Right,
        RoomImageSlot.South => System.Windows.HorizontalAlignment.Center,
        RoomImageSlot.SouthEast => System.Windows.HorizontalAlignment.Right,
        RoomImageSlot.East => System.Windows.HorizontalAlignment.Right,
        RoomImageSlot.SouthWest => System.Windows.HorizontalAlignment.Left,
        RoomImageSlot.West => System.Windows.HorizontalAlignment.Left,
        RoomImageSlot.NorthWest => System.Windows.HorizontalAlignment.Left,
        RoomImageSlot.Up => System.Windows.HorizontalAlignment.Center,
        RoomImageSlot.Down => System.Windows.HorizontalAlignment.Center,
        _ => System.Windows.HorizontalAlignment.Center
    };

    public System.Windows.VerticalAlignment OverlayVerticalAlignment => Entry.Slot switch
    {
        RoomImageSlot.North => System.Windows.VerticalAlignment.Top,
        RoomImageSlot.NorthEast => System.Windows.VerticalAlignment.Top,
        RoomImageSlot.South => System.Windows.VerticalAlignment.Bottom,
        RoomImageSlot.SouthEast => System.Windows.VerticalAlignment.Bottom,
        RoomImageSlot.East => System.Windows.VerticalAlignment.Center,
        RoomImageSlot.SouthWest => System.Windows.VerticalAlignment.Bottom,
        RoomImageSlot.West => System.Windows.VerticalAlignment.Center,
        RoomImageSlot.NorthWest => System.Windows.VerticalAlignment.Top,
        RoomImageSlot.Up => System.Windows.VerticalAlignment.Top,
        RoomImageSlot.Down => System.Windows.VerticalAlignment.Bottom,
        _ => System.Windows.VerticalAlignment.Center
    };

    // Rotate around the slot anchor to avoid drift when width/height swap at 90/270 rotations.
    public System.Windows.Point OverlayRenderTransformOrigin => Entry.Slot switch
    {
        RoomImageSlot.North => new System.Windows.Point(0.5, 0),
        RoomImageSlot.NorthEast => new System.Windows.Point(1, 0),
        RoomImageSlot.East => new System.Windows.Point(1, 0.5),
        RoomImageSlot.SouthEast => new System.Windows.Point(1, 1),
        RoomImageSlot.South => new System.Windows.Point(0.5, 1),
        RoomImageSlot.SouthWest => new System.Windows.Point(0, 1),
        // West needs a center pivot to keep authored 180/-90 rotations visible and stable.
        RoomImageSlot.West => new System.Windows.Point(0.5, 0.5),
        RoomImageSlot.NorthWest => new System.Windows.Point(0, 0),
        RoomImageSlot.Up => new System.Windows.Point(0.5, 0.5),
        RoomImageSlot.Down => new System.Windows.Point(0.5, 0.5),
        _ => new System.Windows.Point(0.5, 0.5)
    };

    public double OverlayOffsetX
    {
        get => Entry.OverlayOffsetX;
        set
        {
            if (Math.Abs(Entry.OverlayOffsetX - value) < 0.0001)
            {
                return;
            }

            Entry.OverlayOffsetX = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public double OverlayOffsetY
    {
        get => Entry.OverlayOffsetY;
        set
        {
            if (Math.Abs(Entry.OverlayOffsetY - value) < 0.0001)
            {
                return;
            }

            Entry.OverlayOffsetY = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public double OverlayRotationDegrees
    {
        get => Entry.OverlayRotationDegrees;
        set
        {
            if (Math.Abs(Entry.OverlayRotationDegrees - value) < 0.0001)
            {
                return;
            }

            Entry.OverlayRotationDegrees = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public int OverlayRenderOrder
    {
        get => Entry.OverlayRenderOrder;
        set
        {
            if (Entry.OverlayRenderOrder == value)
            {
                return;
            }

            Entry.OverlayRenderOrder = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public int DefaultOverlayRenderOrder => RoomImageEntry.GetDefaultOverlayRenderOrder(Entry.Slot);

    public void ResetOverlayRenderOrderToDefault()
    {
        OverlayRenderOrder = DefaultOverlayRenderOrder;
    }

    public string FullImagePath
    {
        get => Entry.Image.FullImagePath;
        set
        {
            if (Entry.Image.FullImagePath == value)
            {
                return;
            }

            Entry.Image.FullImagePath = value;
            RefreshPreview();

            _onEdited?.Invoke();
        }
    }

    public string GrayMapImagePath
    {
        get => Entry.Image.GrayMapImagePath;
        set
        {
            if (Entry.Image.GrayMapImagePath == value)
            {
                return;
            }

            Entry.Image.GrayMapImagePath = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public string NormalMapImagePath
    {
        get => Entry.Image.NormalMapImagePath;
        set
        {
            if (Entry.Image.NormalMapImagePath == value)
            {
                return;
            }

            Entry.Image.NormalMapImagePath = value;
            OnPropertyChanged();
            _onEdited?.Invoke();
        }
    }

    public BitmapImage? VariantThumbnailSource => LoadThumbnail(FullImagePath);

    public BitmapImage? ThumbnailSource
    {
        get => LoadThumbnail(FullImagePath);
    }

    private BitmapImage? LoadThumbnail(string path)
    {
        var resolvedPath = DesignerImagePathResolver.ResolveForPreview(path, _projectFilePathAccessor?.Invoke(), _preferredSourceBucket);
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

    public void SetVariantPaths(string fullPath, string grayPath, string normalPath)
    {
        Entry.Image.FullImagePath = fullPath;
        Entry.Image.GrayMapImagePath = grayPath;
        Entry.Image.NormalMapImagePath = normalPath;
        RefreshPreview();
        OnPropertyChanged(nameof(GrayMapImagePath));
        OnPropertyChanged(nameof(NormalMapImagePath));
    }

    public void RefreshPreview()
    {
        OnPropertyChanged(nameof(FullImagePath));
        OnPropertyChanged(nameof(VariantThumbnailSource));
        OnPropertyChanged(nameof(ThumbnailSource));
        OnPropertyChanged(nameof(HasConfiguredImage));
        OnPropertyChanged(nameof(DirectionStatusText));
    }
}
