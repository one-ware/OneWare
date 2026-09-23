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
                _ when status.HasFlag(FileStatus.Conflicted) => Brushes.Purple,
                _ when (status & (FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir)) != 0 => Brushes.Red,
                _ when (status & (FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir |
                                 FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir |
                                 FileStatus.TypeChangeInIndex | FileStatus.TypeChangeInWorkdir)) != 0 => Brushes.Goldenrod,
                _ when (status & (FileStatus.NewInIndex | FileStatus.NewInWorkdir)) != 0 =>
                    Application.Current?.FindResource("GreenAccent") ?? Brushes.Green,
                _ => Application.Current?.FindResource("ThemeForegroundBrush")
            };
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}