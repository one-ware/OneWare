using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Shared;

namespace OneWare.Core.Converters;

public class FileToTabBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EditViewModel{CurrentFile: ExternalFile}) return new BrushConverter().ConvertFrom("#55fa9820");;
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}