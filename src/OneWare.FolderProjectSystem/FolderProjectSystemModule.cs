using OneWare.FolderProjectSystem.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.FolderProjectSystem;

public class FolderProjectSystemModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider
            .Resolve<IProjectManagerService>()
            .RegisterProjectManager(typeof(FolderProjectRoot), containerProvider.Resolve<FolderProjectManager>());
    }
}