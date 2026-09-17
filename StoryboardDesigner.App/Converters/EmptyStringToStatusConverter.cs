using System.Globalization;
using System.Windows.Data;

namespace StoryboardDesigner.App.Converters;

public sealed class EmptyStringToStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? "Undefined" : "Defined";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
