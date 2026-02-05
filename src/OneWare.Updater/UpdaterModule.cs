using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Updater.ViewModels;
using OneWare.Updater.Views;

namespace OneWare.Updater;

public class UpdaterModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<UpdaterViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var windowService = serviceProvider.Resolve<IWindowService>();
        var vm = serviceProvider.Resolve<UpdaterViewModel>();
        
        if (PlatformHelper.Platform is PlatformId.WinArm64 or PlatformId.WinX64 or PlatformId.OsxX64
            or PlatformId.OsxArm64)
            windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("Update")
            {
                Header = "Studio Update",
                Command = new RelayCommand(() =>
                {
                    windowService.Show(new UpdaterView
                    {
                        DataContext = vm
                    });
                    if (vm.Status == UpdaterStatus.UpdateUnavailable) _ = vm.CheckForUpdateAsync();
                }),
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.DownloadDefault16X")
            });
    }
}