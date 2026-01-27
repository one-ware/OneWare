using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OneWare.UniversalFpgaProjectSystem.Converters;

public class HorizontalLabelMarginConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double i)
            return new Thickness(0 - i, 0, 0, 0);
        return new Thickness(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}