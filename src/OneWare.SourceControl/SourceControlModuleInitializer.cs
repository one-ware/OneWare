using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.Settings;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl
{
    public class SourceControlModuleInitializer
    {
        private readonly ISettingsService _settingsService;
        private readonly IWindowService _windowService;
        private readonly IDockService _dockService;
        private readonly SourceControlViewModel _sourceControlViewModel;

        public SourceControlModuleInitializer(
            ISettingsService settingsService,
            IWindowService windowService,
            IDockService dockService,
            SourceControlViewModel sourceControlViewModel)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
            _sourceControlViewModel = sourceControlViewModel ?? throw new ArgumentNullException(nameof(sourceControlViewModel));
        }

        public void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");

            _settingsService.RegisterCustom("Team Explorer", "GitHub", SourceControlModule.GitHubAccountNameKey, new GitHubAccountSetting());

            _settingsService.RegisterSettingCategory("Team Explorer", 10, "VsImageLib.Team16X");
            _settingsService.RegisterTitled("Team Explorer", "Fetch", "SourceControl_AutoFetchEnable",
                "Auto fetch", "Fetch for changed automatically", true);
            _settingsService.RegisterTitledSlider("Team Explorer", "Fetch", "SourceControl_AutoFetchDelay",
                "Auto fetch interval", "Interval in seconds", 60, 5, 60, 5);
            _settingsService.RegisterTitled("Team Explorer", "Polling", "SourceControl_PollChangesEnable",
                "Poll for changes", "Fetch for changed files automatically", true);
            _settingsService.RegisterTitledSlider("Team Explorer", "Polling", "SourceControl_PollChangesDelay",
                "Poll changes interval", "Interval in seconds", 5, 1, 60, 1);

            _dockService.RegisterLayoutExtension<SourceControlViewModel>(DockShowLocation.Left);

            _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("SourceControl")
            {
                Header = "Source Control",
                Command = new RelayCommand(() => _dockService.Show(_sourceControlViewModel)),
                IconObservable = Application.Current!.GetResourceObservable(SourceControlViewModel.IconKey)
            });
        }
    }
}
