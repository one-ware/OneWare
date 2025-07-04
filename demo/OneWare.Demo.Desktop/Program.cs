using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using OneWare.Core;
using OneWare.Core.Modules;
using OneWare.Core.ViewModels.Windows;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Helpers;
using Prism.Ioc;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OneWare.Demo.Desktop;

internal abstract class Program
{
    // This method is needed for IDE previewer infrastructure
    private static AppBuilder BuildAvaloniaApp()
    {
        var app = AppBuilder.Configure<DesktopDemoApp>().UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                EnableMultiTouch = true
            })
            .With(new Win32PlatformOptions
            {
                WinUICompositionBackdropCornerRadius = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Environment.OSVersion.Version.Build >= 22000 ? 8 : 0
                    : 0
            })
            //.WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://OneWare.Core/Assets/Fonts#Noto Sans"
            })
            .LogToTrace();

        if (DemoApp.SettingsService.GetSettingValue<bool>("Experimental_UseManagedFileDialog"))
            app.UseManagedSystemDialogs();

        return app;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            // Initialize the Autofac container adapter
            var containerAdapter = new AutofacContainerAdapter();

            // Define the path to the plugins folder within the application directory
            string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pluginsFolderPath = Path.Combine(applicationDirectory, "plugins");

            // Ensure the plugins directory exists
            EnsurePluginsDirectoryExists(pluginsFolderPath);

            // Load assemblies from the plugins folder
            var assemblies = LoadAssembliesFromFolder(pluginsFolderPath);

            // Register assemblies with Autofac
            RegisterAssembliesWithAutofac(containerAdapter, assemblies);

            // Register MainWindowViewModel
            containerAdapter.Register<MainWindowViewModel, MainWindowViewModel>(isSingleton: true);

            // Create and load the CoreModuleAdapter
            var coreModuleAdapter = new OneWareCoreModule(containerAdapter);
            coreModuleAdapter.RegisterTypes();

            // Build the container
            containerAdapter.Build();

            // Set the container adapter statically using the class name
            App.SetContainerAdapter(containerAdapter);

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        }
        catch (Exception ex)
        {
            if (ContainerLocator.Container.IsRegistered<ILogger>())
                ContainerLocator.Container?.Resolve<ILogger>()?.Error(ex.Message, ex, false);
            else Console.WriteLine(ex.ToString());

            PlatformHelper.WriteTextFile(
                Path.Combine(DemoApp.Paths.CrashReportsDirectory,
                    "crash_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", DateTimeFormatInfo.InvariantInfo) +
                    ".txt"), ex.ToString());
#if DEBUG
            Console.ReadLine();
#endif
        }

        return 0;
    }

    private static void EnsurePluginsDirectoryExists(string pluginsFolderPath)
    {
        if (!Directory.Exists(pluginsFolderPath))
        {
            Directory.CreateDirectory(pluginsFolderPath);
            Log.Information($"Created plugins directory at {pluginsFolderPath}");
        }
    }

    private static Assembly[] LoadAssembliesFromFolder(string folderPath)
    {
        DirectoryInfo dir = new DirectoryInfo(folderPath);
        return dir.GetFiles("*.dll")
                  .Select(file => Assembly.LoadFrom(file.FullName))
                  .ToArray();
    }

    private static void RegisterAssembliesWithAutofac(IContainerAdapter containerAdapter, Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            containerAdapter.RegisterAssemblyTypes(assembly);
            Log.Information($"Registered assembly: {assembly.FullName}");
        }
    }
}