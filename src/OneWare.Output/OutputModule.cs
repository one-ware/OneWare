using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Output.ViewModels;

namespace OneWare.Output;

public class OutputModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<OutputViewModel>();
        services.AddSingleton<IOutputService>(provider => provider.Resolve<OutputViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<IOutputService>(DockShowLocation.Bottom);

        settingsService.Register("Output_Autoscroll", true);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Output")
        {
            Header = "Output",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IOutputService>())),
            IconObservable = Application.Current!.GetResourceObservable(OutputViewModel.IconKey)
        });
    }
}

