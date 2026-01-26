// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.TestPlugin;

public class TestPluginModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((selected, menuItems) =>
        {
            if (selected is [IProjectFile])
                menuItems.Add(new MenuItemViewModel("Hello World")
                {
                    Header = "Hello World"
                });
        });
    }
}