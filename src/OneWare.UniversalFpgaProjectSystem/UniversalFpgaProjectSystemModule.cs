using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;
using Prism.Modularity;


namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectSystemModule 
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<UniversalFpgaProjectManager>();
        containerRegistry.RegisterSingleton<FpgaService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var containerAdapter = containerProvider.Resolve<IContainerAdapter>();
        var manager = containerAdapter.Resolve<UniversalFpgaProjectManager>();
        var windowService = containerAdapter.Resolve<IWindowService>();
        var settingsService = containerAdapter.Resolve<ISettingsService>();

        settingsService.Register("UniversalFpgaProjectSystem_LongTermProgramming", false);

        containerAdapter
            .Resolve<IProjectManagerService>()
            .RegisterProjectManager(UniversalFpgaProjectRoot.ProjectType, manager);

        containerAdapter.Resolve<ILanguageManager>()
            .RegisterLanguageExtensionLink(UniversalFpgaProjectRoot.ProjectFileExtension, ".json");

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => _ = manager.NewProjectDialogAsync()),
                Icon = SharedConverters.PathToBitmapConverter.Convert(
                    containerAdapter.Resolve<IPaths>().AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => containerAdapter.Resolve<IProjectExplorerService>()
                    .LoadProjectFileDialogAsync(manager,
                        new FilePickerFileType(
                            $"Universal FPGA Project (*{UniversalFpgaProjectRoot.ProjectFileExtension})")
                        {
                            Patterns = [$"*{UniversalFpgaProjectRoot.ProjectFileExtension}"]
                        })),
                Icon = SharedConverters.PathToBitmapConverter.Convert(
                    containerAdapter.Resolve<IPaths>().AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });

        var toolBarViewModel = containerAdapter.Resolve<UniversalFpgaProjectToolBarViewModel>();

        windowService.RegisterMenuItem("MainWindow_MainMenu",
            new MenuItemViewModel("FPGA")
            {
                Header = "FPGA",
                Priority = 200
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/FPGA",
        [
            new MenuItemViewModel("Download")
            {
                Header = "Download",
                Command = new AsyncRelayCommand(() => toolBarViewModel.DownloadAsync()),
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.Download16X")
            },
            new MenuItemViewModel("Compile")
            {
                Header = "Compile",
                Command = new AsyncRelayCommand(() => toolBarViewModel.CompileAsync()),
                IconObservable = Application.Current!.GetResourceObservable("CreateIcon")
            }
        ]);

        windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension",
            new UiExtension(x => new UniversalFpgaProjectToolBarView { DataContext = toolBarViewModel }));

        windowService.RegisterUiExtension("EditView_Top", new UiExtension(x =>
        {
            if (x is IFile)
                return new UniversalFpgaProjectTestBenchToolBarView
                {
                    DataContext =
                        containerAdapter.Resolve<UniversalFpgaProjectTestBenchToolBarViewModel>((typeof(IFile), x))
                };
            return null;
        }));
    }
}