using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class NumberUnequalConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            double.TryParse(parameter.ToString(), out var param);
            double.TryParse(value.ToString(), out var val);
            return Math.Abs(val - param) > 0.1;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}