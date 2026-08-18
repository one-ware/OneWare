using System.Globalization;
using Avalonia.Data.Converters;
using OneWare.Essentials.Services;

namespace OneWare.Debugger.Helpers;

// Stellt einen absoluten Dateipfad relativ zum Wurzelverzeichnis seines Projekts dar,
// z.B. "MeinProjekt/src/top.vhd" statt "C:\dev\MeinProjekt\src\top.vhd". Rein kosmetisch:
// BreakPoint.File bleibt der absolute Pfad,
// den GdbSession an GDB weiterreicht und BreakPointMargin zum Abgleich mit der offenen Datei
// nutzt. Faellt auf den vollen Pfad zurueck, wenn die Datei zu keinem geladenen Projekt gehoert.
public class ProjectRelativePathConverter : IValueConverter
{
    public static readonly ProjectRelativePathConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string fullPath || string.IsNullOrWhiteSpace(fullPath)) return value;

        // Im XAML-Previewer ist der Container nicht gesetzt - dann bleibt es beim vollen Pfad.
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
