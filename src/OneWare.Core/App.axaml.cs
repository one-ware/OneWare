using System.ComponentModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AvaloniaEdit.Rendering;
using OneWare.Essentials.Helpers;
using OneWare.Core.Services;
using OneWare.Core.Views.Windows;
using OneWare.Core.ViewModels.Windows;
using OneWare.Settings.Views;
using OneWare.Settings.ViewModels;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using OneWare.ApplicationCommands.Services;
using OneWare.Essentials.LanguageService;
using OneWare.ProjectSystem.Services;
using OneWare.Core.ModuleLogic;

namespace OneWare.Core;

public class App : Application
{
    public static Autofac.IContainer Container { get; private set; }

    protected bool _tempMode = false;
    protected AggregateModuleCatalog ModuleCatalog { get; } = new();

    protected virtual string GetDefaultLayoutName => "Default";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var builder = new ContainerBuilder();

        // Register Services
        builder.RegisterType<PluginService>().As<IPluginService>().SingleInstance();
        builder.RegisterType<HttpService>().As<IHttpService>().SingleInstance();
        builder.RegisterType<ApplicationCommandService>().As<IApplicationCommandService>().SingleInstance();
        builder.RegisterType<ProjectManagerService>().As<IProjectManagerService>().SingleInstance();
        builder.RegisterType<LanguageManager>().As<ILanguageManager>().SingleInstance();
        builder.RegisterType<ApplicationStateService>().As<IApplicationStateService>().SingleInstance();
        builder.RegisterType<DockService>().As<IDockService>().SingleInstance();
        builder.RegisterType<WindowService>().As<IWindowService>().SingleInstance();
        builder.RegisterType<BackupService>().SingleInstance();
        builder.RegisterType<ChildProcessService>().As<IChildProcessService>().SingleInstance();
        builder.RegisterType<FileIconService>().As<IFileIconService>().SingleInstance();
        builder.RegisterType<EnvironmentService>().As<IEnvironmentService>().SingleInstance();

        // ViewModels
        builder.RegisterType<MainWindowViewModel>().SingleInstance();
        builder.RegisterType<ApplicationSettingsViewModel>().InstancePerDependency();
        builder.RegisterType<AboutViewModel>().InstancePerDependency();

        // Windows
        builder.RegisterType<MainWindow>().SingleInstance();
        builder.RegisterType<ApplicationSettingsView>().InstancePerDependency();
        builder.RegisterType<AboutView>().InstancePerDependency();

        Container = builder.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = Container.Resolve<MainWindow>();
            mainWindow.DataContext = Container.Resolve<MainWindowViewModel>();

            mainWindow.NotificationManager = new WindowNotificationManager(mainWindow)
            {
                Position = NotificationPosition.TopRight,
                Margin = new Thickness(0, 55, 5, 0),
                BorderThickness = new Thickness(0),
                MaxItems = 3
            };

            desktop.MainWindow = mainWindow;

            HookupSettingsObservers();
            VisualLineLinkText.OpenUriEvent.AddClassHandler<Window>((window, args) =>
            {
                PlatformHelper.OpenHyperLink(args.Uri.ToString());
            });
        }
        _ = LoadContentAsync();


        base.OnFrameworkInitializationCompleted();
    }

    protected virtual Task LoadContentAsync()
    {
        return Task.CompletedTask;
    }

    private void HookupSettingsObservers()
    {
        var settingsService = Container.Resolve<ISettingsService>();

        settingsService.GetSettingObservable<string>("Editor_FontFamily").Subscribe(font =>
        {
            if (FontManager.Current.SystemFonts.Contains(font))
            {
                Resources["EditorFont"] = new FontFamily(font);
            }
        });

        settingsService.GetSettingObservable<int>("Editor_FontSize").Subscribe(size =>
        {
            Resources["EditorFontSize"] = (double)size;
        });

        settingsService.GetSettingObservable<string>("General_SelectedTheme").Subscribe(_ =>
        {
            TypeAssistanceIconStore.Instance.Load();
        });
    }

    private async Task TryShutDownAsync(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;

        try
        {
            var dockService = Container.Resolve<IDockService>();
            var unsavedFiles = dockService.OpenFiles
                .Where(f => f.Value is { IsDirty: true })
                .Select(f => f.Value)
                .ToList();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime cds)
            {
                var mainWin = cds.MainWindow;
                if (await WindowHelper.HandleUnsavedFilesAsync(unsavedFiles, mainWin))
                    await ShutdownAsync();
            }
        }
        catch (Exception ex)
        {
            Container.Resolve<ILogger>().Error(ex.Message, ex);
        }
    }

    private async Task ShutdownAsync()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime cds)
        {
            foreach (var window in cds.Windows) window.Hide();
        }

        Container.Resolve<BackupService>().CleanUp();
        await Container.Resolve<LanguageManager>().CleanResourcesAsync();

        if (!_tempMode)
        {
            await Container.Resolve<IProjectExplorerService>().SaveLastProjectsFileAsync();
            Container.Resolve<IDockService>().SaveLayout();
        }

        Container.Resolve<ISettingsService>().Save(Container.Resolve<IPaths>().SettingsPath);
        Container.Resolve<IApplicationStateService>().ExecuteShutdownActions();

        Environment.Exit(0);
    }

}
