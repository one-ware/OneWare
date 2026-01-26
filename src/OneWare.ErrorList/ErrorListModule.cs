using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.ErrorList.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ErrorList;

public class ErrorListModule : OneWareModuleBase
{
    public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
    public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
    public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ErrorListViewModel>();
        services.AddSingleton<IErrorService>(provider => provider.Resolve<ErrorListViewModel>());
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<IErrorService>(DockShowLocation.Bottom);

        settingsService.Register(KeyErrorListFilterMode, 0);
        settingsService.RegisterSetting("Experimental", "Errors", KeyErrorListShowExternalErrors,
            new CheckBoxSetting("Show external errors", false)
            {
                HoverDescription = "Sets if errors from files outside of your project should be visible"
            });
        settingsService.Register(KeyErrorListVisibleSource, 0);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Problems")
        {
            Header = "Problems",
            Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IErrorService>())),
            IconObservable = Application.Current!.GetResourceObservable(ErrorListViewModel.IconKey)
        });
    }
}