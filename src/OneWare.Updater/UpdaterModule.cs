using Avalonia;
using Avalonia.Controls;
using Autofac;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Updater.ViewModels;
using OneWare.Updater.Views;

namespace OneWare.Updater
{
    public class UpdaterModule : Module
    {
        private readonly PlatformHelper _platformHelper;
        private readonly IDockService _dockService;

        // Constructor to inject PlatformHelper
        public UpdaterModule(PlatformHelper platformHelper)
        {
            _platformHelper = platformHelper;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // Register types with Autofac
            builder.RegisterType<UpdaterViewModel>().SingleInstance();
            base.Load(builder);
        }

        public void OnInitialized(IComponentContext container)
        {
            var windowService = container.Resolve<IWindowService>();

            // Use the instance of PlatformHelper to access the Platform property
            if (_platformHelper.Platform is PlatformId.WinArm64 or PlatformId.WinX64 or PlatformId.OsxX64 or PlatformId.OsxArm64)
            {
                windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("Update")
                {
                    Header = "Studio Update",
                    Command = new RelayCommand(() =>
                    {
                        var vm = container.Resolve<UpdaterViewModel>();
                        windowService.Show(new UpdaterView(_dockService)
                        {
                            DataContext = vm
                        });
                        if (vm.Status == UpdaterStatus.UpdateUnavailable)
                        {
                            _ = vm.CheckForUpdateAsync();
                        }
                    }),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.DownloadDefault16X")
                });
            }
        }
    }
}
