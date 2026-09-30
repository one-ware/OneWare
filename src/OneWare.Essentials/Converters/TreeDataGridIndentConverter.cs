using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters;

/// <summary>
///     Converts a TreeDataGrid row indent level to the left padding of the expander cell.
/// </summary>
public class TreeDataGridIndentConverter : IValueConverter
{
    public static TreeDataGridIndentConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int indent ? new Thickness(16 * indent, 0, 0, 0) : new Thickness();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
