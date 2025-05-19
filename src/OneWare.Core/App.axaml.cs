using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using Splat;

namespace OneWare.App;

public class App : Application
{
    public static IContainer Container { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Register Autofac services
        var builder = new ContainerBuilder();

        // Register ILogger — you can replace ConsoleLogger with your implementation
        builder.RegisterType<ConsoleLogger>()
               .As<Serilog.ILogger>()
               .SingleInstance();

        // Register PlatformHelper (which needs ILogger)
        builder.RegisterType<PlatformHelper>()
               .AsSelf()
               .SingleInstance();

        // You can register more services or windows here
        // builder.RegisterType<MainWindow>().AsSelf();

        Container = builder.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Resolve PlatformHelper with ILogger injected
            var platformHelper = Container.Resolve<PlatformHelper>();

            // Hook up hyperlink event
            VisualLineLinkText.OpenUriEvent.AddClassHandler<Window>((window, args) =>
            {
                platformHelper.OpenHyperLink(args.Uri.ToString());
            });

            // Optionally set the main window
            // desktop.MainWindow = Container.Resolve<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
