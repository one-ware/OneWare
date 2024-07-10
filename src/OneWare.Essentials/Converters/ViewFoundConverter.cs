using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters;

public class ViewFoundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (! value?.GetType().FullName?.Contains("ViewModel") ?? false) return false;
        var name =  value?.GetType().AssemblyQualifiedName?.Replace("ViewModel", "View");
        if (name == null) return false;
        var type = Type.GetType(name);
        return type != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}