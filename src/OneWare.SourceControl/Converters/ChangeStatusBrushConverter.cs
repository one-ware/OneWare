using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LibGit2Sharp;

namespace OneWare.SourceControl.Converters;

public class ChangeStatusBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileStatus status)
            return status switch
            {
                FileStatus.Unaltered => Brushes.Transparent,
                FileStatus.Conflicted => Brushes.Purple,
                FileStatus.DeletedFromIndex => Brushes.Red,
                FileStatus.DeletedFromWorkdir => Brushes.Red,
                FileStatus.ModifiedInIndex => (IBrush?)new BrushConverter().ConvertFrom("#FFC107"),
                FileStatus.ModifiedInWorkdir => (IBrush?)new BrushConverter().ConvertFrom("#FFC107"),
                FileStatus.NewInIndex => Application.Current?.FindResource("GreenAccent"),
                FileStatus.NewInWorkdir => Application.Current?.FindResource("GreenAccent"),
                _ => Application.Current?.FindResource("ForegroundColor")
            };
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}