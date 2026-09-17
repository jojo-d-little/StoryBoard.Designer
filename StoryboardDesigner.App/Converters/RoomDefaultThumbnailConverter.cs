using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Converters;

public sealed class RoomDefaultThumbnailConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Room room)
        {
            return null;
        }

        var path = room.Images
            .FirstOrDefault(image => image.Slot == RoomImageSlot.Default)
            ?.Image.FullImagePath;

        var projectFilePath = (System.Windows.Application.Current?.MainWindow?.DataContext as MainWindowViewModel)?.ProjectFilePath;
        var resolvedPath = DesignerImagePathResolver.ResolveForPreview(path, projectFilePath, preferredSourceBucket: "rooms");

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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
