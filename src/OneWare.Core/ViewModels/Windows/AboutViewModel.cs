using System.Reflection;
using System.Runtime.InteropServices;
using OneWare.Core.Data;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public class AboutViewModel : FlexibleWindowViewModelBase
{
    private readonly IPaths _paths;

    public AboutViewModel(IPaths paths)
    {
        Title = $"About {paths.AppName}";
        Id = "AboutWindow";
        _paths = paths;
    }

    public string Icon => _paths.AppIconPath;

    public string AppName => _paths.AppName;

    public string VersionInfo => $"{_paths.AppName} {DateTime.Now.Year}\nVersion {Global.VersionCode} " +
                                 RuntimeInformation.ProcessArchitecture;

    public string VersionInfoBase =>
        $"OneWare.Core {Assembly.GetAssembly(typeof(App))?.GetName().Version?.ToString() ?? "-"}" +
        $"\nOneWare.Essentials {Assembly.GetAssembly(typeof(SharedConverters))?.GetName().Version?.ToString() ?? "-"}";

    public string Platform => "Platform: " + RuntimeInformation.OSDescription;

    public string License => $"{_paths.AppName}\n" +
                             $"© {DateTime.Now.Year} ONE WARE GmbH\n";
}