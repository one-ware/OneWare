using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Autofac;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;

namespace OneWare.UniversalFpgaProjectSystem;

public static class UniversalFpgaProjectSystemModule
{
    public static void Register(ContainerBuilder builder)
    {
        builder.RegisterType<UniversalFpgaProjectManager>().SingleInstance();
        builder.RegisterType<FpgaService>().SingleInstance();
        builder.RegisterType<UniversalFpgaProjectToolBarViewModel>();
        builder.RegisterType<UniversalFpgaProjectTestBenchToolBarViewModel>();
    }

    public static void Initialize(IContainer container)
    {
        var manager = container.Resolve<UniversalFpgaProjectManager>();
        var windowService = container.Resolve<IWindowService>();
        var settingsService = container.Resolve<ISettingsService>();
        var paths = container.Resolve<IPaths>();

        settingsService.Register("UniversalFpgaProjectSystem_LongTermProgramming", false);

        container.Resolve<IProjectManagerService>()
            .RegisterProjectManager(UniversalFpgaProjectRoot.ProjectType, manager);

        container.Resolve<ILanguageManager>()
            .RegisterLanguageExtensionLink(UniversalFpgaProjectRoot.ProjectFileExtension, ".json");

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => _ = manager.NewProjectDialogAsync()),
                Icon = SharedConverters.PathToBitmapConverter.Convert(paths.AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => container.Resolve<IProjectExplorerService>()
                    .LoadProjectFileDialogAsync(manager,
                        new FilePickerFileType(
                            $"Universal FPGA Project (*{UniversalFpgaProjectRoot.ProjectFileExtension})")
                        {
                            Patterns = [$"*{UniversalFpgaProjectRoot.ProjectFileExtension}"]
                        })),
                Icon = SharedConverters.PathToBitmapConverter.Convert(paths.AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });

        var toolBarViewModel = container.Resolve<UniversalFpgaProjectToolBarViewModel>();

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
            new UiExtension(_ => new UniversalFpgaProjectToolBarView { DataContext = toolBarViewModel }));

        windowService.RegisterUiExtension("EditView_Top", new UiExtension(x =>
        {
            if (x is IFile file)
            {
                return new UniversalFpgaProjectTestBenchToolBarView
                {
                    DataContext = container.Resolve<UniversalFpgaProjectTestBenchToolBarViewModel>(
                        new TypedParameter(typeof(IFile), file))
                };
            }

            return null;
        }));

        var langManager = container.Resolve<ILanguageManager>();
        langManager.RegisterLanguageExtensionLink(".tbconf", ".json");
        langManager.RegisterLanguageExtensionLink(".deviceconf", ".json");
    }
}
