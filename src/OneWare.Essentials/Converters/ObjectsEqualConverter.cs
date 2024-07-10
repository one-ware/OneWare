using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class ObjectsEqualConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            for (var i = 0; i < values.Count; i++)
                if (!values[i]?.Equals(values[0]) ?? false)
                    return false;
            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}