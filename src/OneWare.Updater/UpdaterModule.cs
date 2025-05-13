using System;
using Avalonia;
using Avalonia.Controls;
using Autofac;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Updater.ViewModels;
using OneWare.Updater.Views;

namespace OneWare.Updater;

public class UpdaterModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<UpdaterViewModel>().SingleInstance();
    }

    public static void InitializeMenu(IContainer container)
    {
        var windowService = container.Resolve<IWindowService>();

        if (PlatformHelper.Platform is PlatformId.WinArm64 or PlatformId.WinX64 or PlatformId.OsxX64 or PlatformId.OsxArm64)
        {
            windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("Update")
            {
                Header = "Studio Update",
                Command = new RelayCommand(() =>
                {
                    var vm = container.Resolve<UpdaterViewModel>();
                    windowService.Show(new UpdaterView
                    {
                        DataContext = vm
                    });

                    if (vm.Status == UpdaterStatus.UpdateUnavailable)
                        _ = vm.CheckForUpdateAsync();
                }),
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.DownloadDefault16X")
            });
        }
    }
}
