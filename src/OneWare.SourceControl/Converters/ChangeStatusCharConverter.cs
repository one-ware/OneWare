using System.Globalization;
using Avalonia.Data.Converters;
using LibGit2Sharp;

namespace OneWare.SourceControl.Converters;

public class ChangeStatusCharConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileStatus status)
            return status switch
            {
                FileStatus.NewInIndex => "+",
                FileStatus.NewInWorkdir => "+",
                _ => status.ToString()[0] + ""
            };
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}