using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class ObjectNotTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is not Type type) return false;
            return value?.GetType() != type;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}