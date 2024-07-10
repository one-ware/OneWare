using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class BoolToScrollBarVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool v && v)
                return ScrollBarVisibility.Hidden;
            return ScrollBarVisibility.Hidden;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}