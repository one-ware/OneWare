using Autofac;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;

namespace OneWare.PackageManager;

public class PackageManagerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PackageService>().As<IPackageService>().SingleInstance();
        builder.RegisterType<PackageManagerViewModel>().SingleInstance();

        builder.RegisterBuildCallback(container =>
        {
            var windowService = container.Resolve<IWindowService>();
            var settingsService = container.Resolve<ISettingsService>();

            windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemViewModel("Extensions")
            {
                Header = "Extensions",
                Command = new RelayCommand(() => windowService.Show(new PackageManagerView
                {
                    DataContext = container.Resolve<PackageManagerViewModel>()
                })),
                IconObservable = Application.Current!.GetResourceObservable("PackageManager")
            });

            settingsService.RegisterSettingCategory("Package Manager", 0, "PackageManager");

            settingsService.RegisterSetting("Package Manager", "Sources", "PackageManager_Sources", new ListBoxSetting("Custom Package Sources", [])
            {
                MarkdownDocumentation = """
                    Add custom package sources to the package manager. These sources will be used to search for and install packages.
                    You can add either:
                    - A Package Repository
                    - A Direct link to a package manifest
                """,
            });
        });
    }
}
