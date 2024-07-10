using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class AddConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            if(!double.TryParse(parameter.ToString(), out var param)) return null;
            if(!double.TryParse(value.ToString(), out var val)) return null;
            return param + val;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}