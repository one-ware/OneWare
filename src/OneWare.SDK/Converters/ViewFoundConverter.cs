using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Prism.Ioc;

namespace OneWare.SDK.Converters;

public class ViewFoundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value?.GetType()?.AssemblyQualifiedName?.Replace("ViewModel", "View");
        if (name == null) return false;
        var type = Type.GetType(name);
        return type != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}