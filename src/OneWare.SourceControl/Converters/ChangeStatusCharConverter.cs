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
                _ when status.HasFlag(FileStatus.Conflicted) => "U",
                _ when (status & (FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir)) != 0 => "D",
                _ when (status & (FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir)) != 0 => "R",
                _ when (status & (FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir)) != 0 => "M",
                _ when (status & (FileStatus.TypeChangeInIndex | FileStatus.TypeChangeInWorkdir)) != 0 => "T",
                _ when (status & (FileStatus.NewInIndex | FileStatus.NewInWorkdir)) != 0 => "+",
                _ => ""
            };
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}