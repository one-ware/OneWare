using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;
using OneWare.SoftwareProjectSystem.Models;
using OneWare.SoftwareProjectSystem.Services;
using OneWare.SoftwareProjectSystem.ViewModels;

namespace OneWare.SoftwareProjectSystem;

/// <summary>
/// C/C++ software projects (<c>.swproj</c>) with Build, Clean, Run and Debug.
/// </summary>
public class SoftwareProjectSystemModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ToolchainKitService>();
        services.AddSingleton<SoftwareProjectManager>();
        services.AddSingleton<SoftwareProjectActionProvider>();
        services.AddSingleton<SoftwareProjectFactory>();
        services.AddSingleton<SoftwareProjectAiFunctions>();
        services.AddTransient<SoftwareProjectCreatorViewModel>();
        services.AddTransient<SoftwareProjectSettingsViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var manager = serviceProvider.Resolve<SoftwareProjectManager>();
        var windowService = serviceProvider.Resolve<IWindowService>();
        var projectExplorerService = serviceProvider.Resolve<IProjectExplorerService>();

        serviceProvider.Resolve<IProjectManagerService>()
            .RegisterProjectManager(SoftwareProjectRoot.ProjectType, manager);

        serviceProvider.Resolve<IProjectActionService>().RegisterProvider<SoftwareProjectActionProvider>();

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(SoftwareProjectActionProvider.ErrorSource);

        serviceProvider.Resolve<ILanguageManager>()
            .RegisterLanguageExtensionLink(SoftwareProjectRoot.ProjectFileExtension, ".json");

        // The chat is optional (e.g. not in every studio); resolving it unconditionally would build an unused provider.
        if (serviceProvider.IsRegistered<IAiFunctionProvider>())
            serviceProvider.Resolve<SoftwareProjectAiFunctions>().Register(serviceProvider.Resolve<IAiFunctionProvider>());

        var fileType = new FilePickerFileType($"C/C++ Project (*{SoftwareProjectRoot.ProjectFileExtension})")
        {
            Patterns = [$"*{SoftwareProjectRoot.ProjectFileExtension}"]
        };

        var welcomeScreenService = serviceProvider.Resolve<IWelcomeScreenService>();
        welcomeScreenService.RegisterItemToNew("new_software_project",
            new WelcomeScreenStartItem("new_software_project", "New C/C++ Project...",
                new AsyncRelayCommand(manager.NewProjectDialogAsync))
            {
                Icon = new IconModel("SoftwareProject")
            });
        welcomeScreenService.RegisterItemToOpen("open_software_project",
            new WelcomeScreenStartItem("open_software_project", "Open C/C++ Project...",
                new AsyncRelayCommand(() => projectExplorerService.LoadProjectFileDialogAsync(manager, fileType)))
            {
                Icon = new IconModel("SoftwareProject")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemModel("SoftwareProject")
            {
                Header = "C/C++ Project",
                Command = new AsyncRelayCommand(manager.NewProjectDialogAsync),
                Priority = 2,
                Icon = new IconModel("SoftwareProject")
            });
        windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("SoftwareProject")
            {
                Header = "C/C++ Project",
                Command = new AsyncRelayCommand(() => projectExplorerService.LoadProjectFileDialogAsync(manager, fileType)),
                Icon = new IconModel("SoftwareProject")
            });
    }
}
