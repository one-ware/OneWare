using System.Globalization;
using Avalonia.Data.Converters;
using OneWare.Essentials.Services;

namespace OneWare.Debugger.Helpers;

// Shows an absolute file path relative to the root directory of its project,
// e.g. "MyProject/src/top.vhd" instead of "C:\dev\MyProject\src\top.vhd". Purely cosmetic:
// BreakPoint.File stays the absolute path
// that GdbSession passes on to GDB and BreakPointMargin uses to match against the open
// file. Falls back to the full path if the file belongs to no loaded project.
public class ProjectRelativePathConverter : IValueConverter
{
    public static readonly ProjectRelativePathConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string fullPath || string.IsNullOrWhiteSpace(fullPath)) return value;

        // In the XAML previewer the container is not set - then it stays the full path.
        if (ContainerLocator.Container?.GetService(typeof(IProjectExplorerService))
            is not IProjectExplorerService projectExplorer) return fullPath;

        var root = projectExplorer.GetRootFromFile(fullPath);
        if (root is null) return fullPath;

        var relativePath = Path.GetRelativePath(root.RootFolderPath, fullPath).Replace('\\', '/');
        return $"{root.Name}/{relativePath}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
