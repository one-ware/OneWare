using System.Globalization;
using Avalonia.Data.Converters;
using OneWare.Essentials.Extensions;

namespace OneWare.Essentials.Converters
{
    public class PathsEqualConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            for (var i = 1; i < values.Count; i++)
            {
                if (values[i] is not string p1 || values[0] is not string p2)
                    return false;
                if (!p1.EqualPaths(p2))
                    return false;
            }
            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}