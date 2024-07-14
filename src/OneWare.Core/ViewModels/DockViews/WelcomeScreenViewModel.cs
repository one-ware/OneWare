using System.Runtime.InteropServices;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.Data;
using OneWare.Essentials.Services;

namespace OneWare.Core.ViewModels.DockViews;

public class WelcomeScreenViewModel : Document
{
    private readonly IPaths _paths;

    public WelcomeScreenViewModel(IPaths paths)
    {
        _paths = paths;
        Id = "WelcomeScreen";
        Title = "Welcome";
    }

    public string Icon => _paths.AppIconPath;

    public string AppName => _paths.AppName;

    public string VersionInfo => $"{_paths.AppName} {DateTime.Now.Year} Preview\nVersion {Global.VersionCode} " +
                                 RuntimeInformation.ProcessArchitecture;

    public string Platform => "Platform: " + RuntimeInformation.OSDescription;

    public string License => $"{_paths.AppName} Preview\n" +
                             $"© {DateTime.Now.Year} One Ware\n";
}