using CommunityToolkit.Mvvm.Input;
using OneWare.NetListSvgIntegration.Services;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.NetListSvgIntegration;

public class NetListSvgIntegrationModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<NetListSvgService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var netListSvgService = containerProvider.Resolve<NetListSvgService>();
        
        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu(x =>
        {
            if (x is [IProjectFile {Extension: ".json"} json])
            {
                return new[]
                {
                    new MenuItemModel("NetListSvg")
                    {
                        Header = "NetListSvg",
                        Command = new AsyncRelayCommand(() => netListSvgService.ShowSchemeAsync(json))
                    }
                };
            }
            return null;
        });
    }
}