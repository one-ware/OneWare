using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace OneWare.UniversalFpgaProjectSystem.Converters;

public class ObjectNotNullToStarGridLengthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? GridLength.Star : parameter is GridLength ? (GridLength)parameter : default;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}