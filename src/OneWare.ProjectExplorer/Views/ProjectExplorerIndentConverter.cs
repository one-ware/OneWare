using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OneWare.ProjectExplorer.Views;

public class ProjectExplorerIndentConverter : IValueConverter
{
    public static ProjectExplorerIndentConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int indent) return new Thickness(16 * indent, 0, 0, 0);
        return new Thickness();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}