// OneWare.UniversalFpgaProjectSystem/UniversalFpgaProjectSystemModuleInitializer.cs
// No changes needed here, as it already relies on the injected Func delegates.
// It directly calls the Func, so no `Resolve` appears in this class.

using System;
using System.Collections.Generic;
using System.Drawing;
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
using IFile = OneWare.Essentials.Models.IFile;


namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectSystemModuleInitializer
{
    private readonly UniversalFpgaProjectManager _projectManager;
    private readonly IWindowService _windowService;
    private readonly ISettingsService _settingsService;
    private readonly IProjectManagerService _projectManagerService;
    private readonly ILanguageManager _languageManager;
    private readonly IPaths _paths;
    private readonly UniversalFpgaProjectToolBarViewModel _toolBarViewModel;
    private readonly IProjectExplorerService _projectExplorerService;

    private readonly Func<IFile, UniversalFpgaProjectTestBenchToolBarViewModel> _testBenchToolBarVmFactory;

    public UniversalFpgaProjectSystemModuleInitializer(
        UniversalFpgaProjectManager projectManager,
        IWindowService windowService,
        ISettingsService settingsService,
        IProjectManagerService projectManagerService,
        ILanguageManager languageManager,
        IPaths paths,
        UniversalFpgaProjectToolBarViewModel toolBarViewModel,
        IProjectExplorerService projectExplorerService,
        Func<IFile, UniversalFpgaProjectTestBenchToolBarViewModel> testBenchToolBarVmFactory)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _projectManagerService = projectManagerService ?? throw new ArgumentNullException(nameof(projectManagerService));
        _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _toolBarViewModel = toolBarViewModel ?? throw new ArgumentNullException(nameof(toolBarViewModel));
        _projectExplorerService = projectExplorerService ?? throw new ArgumentNullException(nameof(projectExplorerService));
        _testBenchToolBarVmFactory = testBenchToolBarVmFactory ?? throw new ArgumentNullException(nameof(testBenchToolBarVmFactory));
    }

    public void Initialize()
    {
        _settingsService.Register("UniversalFpgaProjectSystem_LongTermProgramming", false);

        _projectManagerService.RegisterProjectManager(UniversalFpgaProjectRoot.ProjectType, _projectManager);

        _languageManager.RegisterLanguageExtensionLink(UniversalFpgaProjectRoot.ProjectFileExtension, ".json");

        _windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => _ = _projectManager.NewProjectDialogAsync()),
                Icon = SharedConverters.PathToBitmapConverter.Convert(
                    _paths.AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });

        _windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemViewModel("FpgaProject")
            {
                Header = "Project",
                Command = new AsyncRelayCommand(() => _projectExplorerService
                    .LoadProjectFileDialogAsync(_projectManager,
                        new FilePickerFileType(
                            $"Universal FPGA Project (*{UniversalFpgaProjectRoot.ProjectFileExtension})")
                        {
                            Patterns = [$"*{UniversalFpgaProjectRoot.ProjectFileExtension}"]
                        })),
                Icon = SharedConverters.PathToBitmapConverter.Convert(
                    _paths.AppIconPath, typeof(Bitmap), null, null) as Bitmap
            });

        _windowService.RegisterMenuItem("MainWindow_MainMenu",
            new MenuItemViewModel("FPGA")
            {
                Header = "FPGA",
                Priority = 200
            });

        _windowService.RegisterMenuItem("MainWindow_MainMenu/FPGA",
        [
            new MenuItemViewModel("Download")
            {
                Header = "Download",
                Command = new AsyncRelayCommand(() => _toolBarViewModel.DownloadAsync()),
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.Download16X")
            },
            new MenuItemViewModel("Compile")
            {
                Header = "Compile",
                Command = new AsyncRelayCommand(() => _toolBarViewModel.CompileAsync()),
                IconObservable = Application.Current!.GetResourceObservable("CreateIcon")
            }
        ]);

        _windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension",
            new UiExtension(x => new UniversalFpgaProjectToolBarView { DataContext = _toolBarViewModel }));

        _windowService.RegisterUiExtension("EditView_Top", new UiExtension(x =>
        {
            if (x is IFile file)
                return new UniversalFpgaProjectTestBenchToolBarView
                {
                    DataContext = _testBenchToolBarVmFactory(file)
                };
            return null;
        }));
    }
}