using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace StoryboardDesigner.App.Converters;

public sealed class CommandInVocabularyConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var entered = values.Length > 0 ? values[0]?.ToString() ?? string.Empty : string.Empty;
        var vocabulary = values.Length > 1 ? values[1] as IEnumerable : null;

        if (string.IsNullOrWhiteSpace(entered))
        {
            return true;
        }

        if (vocabulary is null)
        {
            return false;
        }

        foreach (var item in vocabulary)
        {
            if (item is null)
            {
                continue;
            }

            if (string.Equals(item.ToString(), entered, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
