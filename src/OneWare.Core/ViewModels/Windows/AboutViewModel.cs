using System.Runtime.InteropServices;
using OneWare.Core.Data;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.Core.ViewModels.Windows
{
    public class AboutViewModel : FlexibleWindowViewModelBase
    {
        private readonly IPaths _paths;

        public string Icon => _paths.AppIconPath;

        public string AppName => _paths.AppName;
        
        public string VersionInfo => $"{_paths.AppName} {DateTime.Now.Year} Preview\nVersion {Global.VersionCode} " +
                                     RuntimeInformation.ProcessArchitecture;

        public string Platform => "Platform: " + RuntimeInformation.OSDescription;

        public string License => $"{_paths.AppName} Preview\n" +
                                 $"© {DateTime.Now.Year} Protop Solutions UG\n";

        public AboutViewModel(IPaths paths)
        {
            Title = $"About {paths.AppName}";
            Id = "AboutWindow";
            _paths = paths;
        }
    }
}