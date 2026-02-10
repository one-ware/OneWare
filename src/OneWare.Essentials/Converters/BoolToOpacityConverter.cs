using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters;

public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true) return 1d;
        if(parameter != null && double.TryParse(parameter.ToString(), out var opacity)) return opacity;
        return 0d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}