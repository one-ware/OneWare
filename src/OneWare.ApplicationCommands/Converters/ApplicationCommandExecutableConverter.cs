using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.LogicalTree;
using OneWare.SDK.Models;

namespace OneWare.ApplicationCommands.Converters;

public class ApplicationCommandExecutableConverter : IMultiValueConverter
{
    public static ApplicationCommandExecutableConverter Instance { get; } = new();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [IApplicationCommand command, ILogical logical])
        {
            return command.CanExecute(logical);
        }
        return false;
    }
}