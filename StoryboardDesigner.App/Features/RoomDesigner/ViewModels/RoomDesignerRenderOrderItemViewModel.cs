using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Globalization;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class RoomDesignerRenderOrderItemViewModel : ViewModelBase
{
    private int _drawOrderZIndex;

    public RoomDesignerRenderOrderItemViewModel(Room room, RoomDesignerPreviewObjectViewModel previewObject, int drawOrderZIndex)
    {
        Room = room;
        PreviewObject = previewObject;
        _drawOrderZIndex = drawOrderZIndex;
        PreviewObject.PropertyChanged += PreviewObject_OnPropertyChanged;
    }

    public RoomDesignerRenderOrderItemViewModel(Room room, RoomSpawnPoint spawnPoint, string spawnPointLabel, string spawnPointMarkerText, int drawOrderZIndex)
    {
        Room = room;
        IsSpawnPoint = true;
        SpawnPoint = spawnPoint;
        SpawnPointLabel = spawnPointLabel;
        SpawnPointMarkerText = spawnPointMarkerText;
        _drawOrderZIndex = drawOrderZIndex;
    }

    private Room Room { get; }

    public RoomDesignerPreviewObjectViewModel? PreviewObject { get; }

    public bool IsSpawnPoint { get; }

    public RoomSpawnPoint? SpawnPoint { get; }

    public string SpawnPointLabel { get; } = "Player Spawn Point";

    public string SpawnPointMarkerText { get; } = string.Empty;

    public bool HasObjectPreview => PreviewObject is not null;

    public string DisplayName => IsSpawnPoint
        ? SpawnPoint?.IsDefault == true ? "Player Spawn Point (Default)" : $"Player Spawn Point ({SpawnPointMarkerText})"
        : PreviewObject!.DisplayName;

    public string ToolTipText => IsSpawnPoint
        ? $"{SpawnPointLabel}. Render Z-order: {SpawnPoint?.RenderZOrder?.ToString(CultureInfo.InvariantCulture) ?? "default"}."
        : DisplayName;

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
            if (PreviewObject is not null)
            {
                PreviewObject.DrawOrderZIndex = value;
            }

            OnPropertyChanged();
        }
    }

    public double PositionX => IsSpawnPoint ? SpawnPoint!.X : PreviewObject!.PositionX;

    public double PositionY => IsSpawnPoint ? SpawnPoint!.Y : PreviewObject!.PositionY;

    public double FootprintWidthPixels => IsSpawnPoint ? 24 : PreviewObject!.FootprintWidthPixels;

    public double FootprintHeightPixels => IsSpawnPoint ? 24 : PreviewObject!.FootprintHeightPixels;

    public double PreviewRenderedIconWidthPixels => PreviewObject?.PreviewRenderedIconWidthPixels ?? 0;

    public double PreviewRenderedIconHeightPixels => PreviewObject?.PreviewRenderedIconHeightPixels ?? 0;

    public double ImageLocalAlignmentOffsetX => PreviewObject?.ImageLocalAlignmentOffsetX ?? 0;

    public double ImageLocalAlignmentOffsetY => PreviewObject?.ImageLocalAlignmentOffsetY ?? 0;

    public double ImageRotationDegrees => PreviewObject?.ImageRotationDegrees ?? 0;

    public BitmapSource? ThumbnailSource => PreviewObject?.ThumbnailSource;

    public bool IsLightOnly => PreviewObject?.IsLightOnly ?? false;

    public bool IsSelected => PreviewObject?.IsSelected ?? false;

    public bool IsRenderable => IsSpawnPoint || (PreviewObject?.IsRenderable ?? false);

    public bool IncludeInPreview
    {
        get => IsSpawnPoint || (PreviewObject?.IncludeInPreview ?? false);
        set
        {
            if (PreviewObject is not null)
            {
                PreviewObject.IncludeInPreview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRenderable));
            }
        }
    }

    public IReadOnlyList<string> AvailablePreviewVariantNames =>
        PreviewObject?.AvailablePreviewVariantNames ?? Array.Empty<string>();

    public ICommand? SelectPreviewVariantCommand => PreviewObject?.SelectPreviewVariantCommand;

    public void NotifySpawnPointChanged()
    {
        if (!IsSpawnPoint)
        {
            return;
        }

        OnPropertyChanged(nameof(PositionX));
        OnPropertyChanged(nameof(PositionY));
        OnPropertyChanged(nameof(ToolTipText));
    }

    public void Detach()
    {
        if (PreviewObject is not null)
        {
            PreviewObject.PropertyChanged -= PreviewObject_OnPropertyChanged;
        }
    }

    private void PreviewObject_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ToolTipText));
        OnPropertyChanged(nameof(PositionX));
        OnPropertyChanged(nameof(PositionY));
        OnPropertyChanged(nameof(FootprintWidthPixels));
        OnPropertyChanged(nameof(FootprintHeightPixels));
        OnPropertyChanged(nameof(PreviewRenderedIconWidthPixels));
        OnPropertyChanged(nameof(PreviewRenderedIconHeightPixels));
        OnPropertyChanged(nameof(ImageLocalAlignmentOffsetX));
        OnPropertyChanged(nameof(ImageLocalAlignmentOffsetY));
        OnPropertyChanged(nameof(ImageRotationDegrees));
        OnPropertyChanged(nameof(ThumbnailSource));
        OnPropertyChanged(nameof(IsLightOnly));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(IsRenderable));
        OnPropertyChanged(nameof(IncludeInPreview));
        OnPropertyChanged(nameof(AvailablePreviewVariantNames));
        OnPropertyChanged(nameof(SelectPreviewVariantCommand));
    }
}
