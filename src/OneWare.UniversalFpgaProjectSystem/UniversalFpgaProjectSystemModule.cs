using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using OneWare.Shared.Converters;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectSystemModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var manager = containerProvider.Resolve<UniversalFpgaProjectManager>();
        
        containerProvider
            .Resolve<IProjectManagerService>()
            .RegisterProjectManager(UniversalFpgaProjectRoot.ProjectType, manager);

        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => _ = manager.NewProjectDialogAsync()),
                ImageIcon = SharedConverters.PathToBitmapConverter.Convert(ContainerLocator.Container.Resolve<IPaths>().AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });
        
        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => containerProvider.Resolve<IProjectExplorerService>().LoadProjectFileDialogAsync(manager, 
                    new FilePickerFileType("Universal FPGA Project (*.fpgaproj)")
                {
                    Patterns = new[] { "*.fpgaproj" }
                })),
                ImageIcon = SharedConverters.PathToBitmapConverter.Convert(ContainerLocator.Container.Resolve<IPaths>().AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });
    }
}