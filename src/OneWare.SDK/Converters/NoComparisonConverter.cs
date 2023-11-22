using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.SDK.Converters
{
    public class NoComparisonConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !value?.Equals(parameter);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !value?.Equals(true) == true ? parameter : null;
        }
    }
}