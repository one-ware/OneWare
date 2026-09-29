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
                _ when status.HasFlag(FileStatus.Conflicted) => GetBrush("ErrorBrush"),
                _ when (status & (FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir)) != 0 =>
                    GetBrush("ErrorBrush"),
                _ when (status & (FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir |
                                 FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir |
                                 FileStatus.TypeChangeInIndex | FileStatus.TypeChangeInWorkdir)) != 0 => GetBrush("WarningBrush"),
                _ when (status & (FileStatus.NewInIndex | FileStatus.NewInWorkdir)) != 0 =>
                                 GetBrush("SuccessBrush"),
                _ => GetBrush("ThemeForegroundLowBrush")
            };
        return null;
    }

    private static IBrush GetBrush(string key)
    {
        return Application.Current?.FindResource(key) as IBrush ?? Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}