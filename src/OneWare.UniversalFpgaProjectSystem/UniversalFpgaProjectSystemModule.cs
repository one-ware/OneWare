using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectSystemModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<UniversalFpgaProjectManager>();
        services.AddSingleton<FpgaService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var manager = serviceProvider.Resolve<UniversalFpgaProjectManager>();
        var windowService = serviceProvider.Resolve<IWindowService>();
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var welcomeScreenService = serviceProvider.Resolve<IWelcomeScreenService>();

        welcomeScreenService.RegisterItemToNew("new_project",
            new WelcomeScreenStartItem("new_file", "New FPGA Project...",
                new AsyncRelayCommand(() => _ = manager.NewProjectDialogAsync()))
            {
                IconModel = new IconModel("UniversalProject")
            });

        welcomeScreenService.RegisterItemToOpen("open_project",
            new WelcomeScreenStartItem("open_project", "Open FPGA project...", new AsyncRelayCommand(() =>
                serviceProvider.Resolve<IProjectExplorerService>()
                    .LoadProjectFileDialogAsync(manager,
                        new FilePickerFileType(
                            $"Project (*{UniversalFpgaProjectRoot.ProjectFileExtension})")
                        {
                            Patterns = [$"*{UniversalFpgaProjectRoot.ProjectFileExtension}"]
                        })))
            {
                IconModel = new IconModel("UniversalProject")
            });

        settingsService.Register("UniversalFpgaProjectSystem_LongTermProgramming", false);

        serviceProvider.Resolve<IProjectManagerService>()
            .RegisterProjectManager(UniversalFpgaProjectRoot.ProjectType, manager);

        serviceProvider.Resolve<ILanguageManager>()
            .RegisterLanguageExtensionLink(UniversalFpgaProjectRoot.ProjectFileExtension, ".json");

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "FPGA Project",
                Command = new AsyncRelayCommand(() => _ = manager.NewProjectDialogAsync()),
                Priority = 1,
                IconModel = new IconModel("UniversalProject")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "FPGA Project",
                Command = new AsyncRelayCommand(() => serviceProvider.Resolve<IProjectExplorerService>()
                    .LoadProjectFileDialogAsync(manager,
                        new FilePickerFileType(
                            $"Project (*{UniversalFpgaProjectRoot.ProjectFileExtension})")
                        {
                            Patterns = [$"*{UniversalFpgaProjectRoot.ProjectFileExtension}"]
                        })),
                IconModel = new IconModel("UniversalProject")
            });

        var toolBarViewModel = serviceProvider.Resolve<UniversalFpgaProjectToolBarViewModel>();

        windowService.RegisterMenuItem("MainWindow_MainMenu",
            new MenuItemViewModel("FPGA")
            {
                Header = "FPGA",
                Priority = 200
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/FPGA", new MenuItemViewModel("Download")
        {
            Header = "Download",
            Command = new AsyncRelayCommand(() => toolBarViewModel.DownloadAsync()),
            IconModel = new IconModel("VsImageLib.Download16X")
        }, new MenuItemViewModel("Compile")
        {
            Header = "Compile",
            Command = new AsyncRelayCommand(() => toolBarViewModel.CompileAsync()),
            IconModel = new IconModel("CreateIcon")
        });

        windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension",
            new OneWareUiExtension(x => new UniversalFpgaProjectToolBarView { DataContext = toolBarViewModel }));

        windowService.RegisterUiExtension("EditView_Top", new OneWareUiExtension(x =>
        {
            if (x is string fullPath)
                return new UniversalFpgaProjectTestBenchToolBarView
                {
                    DataContext = serviceProvider.Resolve<UniversalFpgaProjectTestBenchToolBarViewModel>(
                        (typeof(string), fullPath))
                };
            return null;
        }));

        serviceProvider.Resolve<ILanguageManager>().RegisterLanguageExtensionLink(".tbconf", ".json");
        serviceProvider.Resolve<ILanguageManager>().RegisterLanguageExtensionLink(".deviceconf", ".json");
    }
}
