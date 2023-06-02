using System.Runtime.InteropServices;
using OneWare.Core.Data;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Core.ViewModels.Windows
{
    public class InfoWindowViewModel : ViewModelBase
    {
        private readonly IPaths _paths;

        public string Icon => _paths.AppIconPath;

        public string AppName => _paths.AppName;
        
        public string VersionInfo => $"{_paths.AppName} {DateTime.Now.Year} Preview\nVersion {Global.VersionCode} " +
                                     RuntimeInformation.ProcessArchitecture;

        public string Platform => "Platform: " + RuntimeInformation.OSDescription;

        public string License => $"{_paths.AppName} Preview\n" +
                                 $"© {DateTime.Now.Year} Protop Solutions UG\n";

        public InfoWindowViewModel(IPaths paths)
        {
            _paths = paths;
        }
    }
}