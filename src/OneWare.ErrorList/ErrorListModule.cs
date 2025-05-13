using System;
using Avalonia;
using Avalonia.Controls;
using Autofac;
using CommunityToolkit.Mvvm.Input;
using OneWare.ErrorList.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ErrorList;

public class ErrorListModule : Module
{
    public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
    public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
    public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";

    protected override void Load(ContainerBuilder builder)
    {
        // Register ErrorListViewModel as singleton for multiple interfaces
        builder.RegisterType<ErrorListViewModel>()
               .As<IErrorService>()
               .AsSelf()
               .SingleInstance();
    }

    public static void Load(ILifetimeScope container)
    {
        var dockService = container.Resolve<IDockService>();
        var settingsService = container.Resolve<ISettingsService>();
        var windowService = container.Resolve<IWindowService>();
        var errorService = container.Resolve<IErrorService>();

        dockService.RegisterLayoutExtension<IErrorService>(DockShowLocation.Bottom);

        settingsService.Register(KeyErrorListFilterMode, 0);
        settingsService.RegisterTitled("Experimental", "Errors", KeyErrorListShowExternalErrors,
            "Show external errors", "Sets if errors from files outside of your project should be visible", false);
        settingsService.Register(KeyErrorListVisibleSource, 0);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Problems")
        {
            Header = "Problems",
            Command = new RelayCommand(() => dockService.Show(errorService)),
            IconObservable = Application.Current!.GetResourceObservable(ErrorListViewModel.IconKey)
        });
    }
}
