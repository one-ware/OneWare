using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class FileOpacityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true)
                return 0.6d;
            return 1d;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}