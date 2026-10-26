using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.ProjectActions;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectExplorer.Views;

namespace OneWare.ProjectExplorer;

public class ProjectExplorerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IFileWatchService, FileWatchService>();
        services.AddSingleton<ProjectExplorerViewModel>();
        services.AddSingleton<IProjectExplorerService>(provider => provider.Resolve<ProjectExplorerViewModel>());
        services.AddSingleton<IProjectActionService, ProjectActionService>();
        services.AddSingleton<ProjectActionToolBarViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        if (serviceProvider.Resolve<IProjectExplorerService>() is not ProjectExplorerViewModel vm) return;

        var dockService = serviceProvider.Resolve<IMainDockService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<IProjectExplorerService>(DockShowLocation.Left);

        windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new OneWareUiExtension(_ =>
            new ProjectExplorerMainWindowToolBarExtension
            {
                DataContext = vm
            }));

        var actionToolBarViewModel = serviceProvider.Resolve<ProjectActionToolBarViewModel>();

        windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension", new OneWareUiExtension(_ =>
            new ProjectActionToolBarView
            {
                DataContext = actionToolBarViewModel
            }));

        RegisterProjectActionMenu(windowService, serviceProvider.Resolve<IProjectActionService>(), vm);

        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("File")
        {
            Priority = -10,
            Header = "File"
        });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.OpenFileDialogAsync()),
                Icon = new IconModel("VsImageLib.NewFileCollection16X")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemModel("File")
            {
                Header = "File",
                Command = new RelayCommand(() => _ = vm.ImportFileDialogAsync()),
                Priority = 10,
                Icon = new IconModel("VsImageLib.NewFileCollection16X")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Project Explorer")
            {
                Header = "Project Explorer",
                Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<IProjectExplorerService>(), DockShowLocation.Left)),
                Icon = new IconModel(ProjectExplorerViewModel.IconKey)
            });
    }

    // The Build menu works for every project type, including those with their own toolbar (e.g. FPGA).
    private static void RegisterProjectActionMenu(IWindowService windowService, IProjectActionService actionService,
        IProjectExplorerService projectExplorerService)
    {
        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("Build")
        {
            Header = "Build",
            Priority = 150
        });

        var items = new (ProjectActionKind Kind, string Header, string Icon, KeyGesture? Gesture)[]
        {
            (ProjectActionKind.Build, "Build", "CreateIcon",
                new KeyGesture(Key.B, KeyModifiers.Shift | PlatformHelper.ControlKey)),
            (ProjectActionKind.Clean, "Clean", "Material.DeleteSweep", null),
            (ProjectActionKind.Run, "Run", "MaterialDesign.PlayArrow", new KeyGesture(Key.F5, PlatformHelper.ControlKey)),
            (ProjectActionKind.Program, "Program", "VsImageLib.Download16X", null),
            (ProjectActionKind.Debug, "Debug", "Codeicons.Debug", new KeyGesture(Key.F5))
        };

        var models = items.Select(item => (item.Kind, Model: new MenuItemModel(item.Header)
        {
            Header = item.Header,
            Command = new AsyncRelayCommand(() => actionService.ExecuteAsync(item.Kind)),
            Icon = new IconModel(item.Icon),
            InputGesture = item.Gesture
        })).ToArray();

        void UpdateEnabled()
        {
            foreach (var (kind, model) in models)
                model.IsEnabled = !actionService.IsBusy && actionService.CanExecute(kind);
        }

        actionService.StateChanged += (_, _) => Dispatcher.UIThread.Post(UpdateEnabled);
        projectExplorerService.WhenValueChanged(x => x.ActiveProject).Subscribe(_ => UpdateEnabled());

        windowService.RegisterMenuItem("MainWindow_MainMenu/Build", models.Select(x => x.Model).ToArray());
    }
}