using CommunityToolkit.Mvvm.Input;
using OneWare.Ghdl.Services;
using OneWare.Shared;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Ghdl;

public class GhdlModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var ghdlService = containerProvider.Resolve<GhdlService>();
        
        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/Simulator", new MenuItemModel("SimulateGHDL")
        {
            Header = "Simulate with GHDL",
            Command = new AsyncRelayCommand(async () =>
            {
                if(containerProvider.Resolve<IProjectExplorerService>()
                       .SelectedItems.First() is IProjectFile selectedFile)
                    await ghdlService.SimulateFileAsync(selectedFile);
            }, () => containerProvider.Resolve<IProjectExplorerService>()
                .SelectedItems.FirstOrDefault() is IProjectFile),
        });
    }
}