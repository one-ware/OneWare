using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Output.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Output;

public class OutputModule : IModule
{
    private readonly IMainDockService _mainDockService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    public OutputModule(ISettingsService settingsService, IMainDockService mainDockService, IWindowService windowService)
    {
        _settingsService = settingsService;
        _windowService = windowService;
        _mainDockService = mainDockService;
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterManySingleton<OutputViewModel>(typeof(IOutputService),
            typeof(OutputViewModel));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        _mainDockService.RegisterLayoutExtension<IOutputService>(DockShowLocation.Bottom);

        _settingsService.Register("Output_Autoscroll", true);

        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Output")
        {
            Header = "Output",
            Command = new RelayCommand(() => _mainDockService.Show(containerProvider.Resolve<IOutputService>())),
            IconObservable = Application.Current!.GetResourceObservable(OutputViewModel.IconKey)
        });
    }
}