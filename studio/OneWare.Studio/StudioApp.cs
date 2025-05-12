using Autofac;
using Avalonia.Markup.Xaml.Styling;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.CruviAdapterExtensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vcd.Viewer;
using OneWare.Vcd.Viewer.ViewModels;
using Prism.Modularity;

namespace OneWare.Studio
{
    public class StudioApp : App
    {
        public static readonly ISettingsService SettingsService = new SettingsService();
        public static readonly IProjectSettingsService ProjectSettingsService = new ProjectSettingsService();
        public static readonly IPaths Paths = new Paths("OneWare Studio", "avares://OneWare.Studio/Assets/icon.ico");
        private static readonly ILogger Logger = new Logger(Paths);

        static StudioApp()
        {
            // Register settings and configuration
            SettingsService.Register("LastVersion", Global.VersionCode);
            SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
            SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog",
                "Use Managed File Dialog (restart required)",
                "On some linux distros, the default file dialog is not available or will crash the app. Use this option to fix this issue. Restart required to apply this setting!",
                false);
            SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_AutoDownloadBinaries",
                "Automatically download Binaries",
                "Automatically download binaries for features when possible", true);
            SettingsService.Load(Paths.SettingsPath);
        }

        // Container to hold resolved instances
        public static IContainer Container { get; private set; }

        public override void Initialize()
        {
            base.Initialize();

            // Setup the Autofac container here
            var builder = new ContainerBuilder();

            // Register services
            builder.RegisterInstance(SettingsService);
            builder.RegisterInstance(ProjectSettingsService);
            builder.RegisterInstance(Paths);
            builder.RegisterInstance(Logger);

            // Register services (Singletons)
            builder.RegisterType<ThemeManager>().AsSelf().SingleInstance();

            // Register your Autofac module (instead of RegisterType)

            UniversalFpgaProjectSystemModule.Register(builder);
            VcdViewerModule.Register(builder);
            CruviAdapterExtensionsModule.Register(builder);

            // Build the container
            Container = builder.Build();

            // Resolve ThemeManager instance and apply styles
            var themeManager = Container.Resolve<ThemeManager>();

            // Apply the styles
            Styles.Add(new StyleInclude(new Uri("avares://OneWare.Studio"))
            {
                Source = new Uri("avares://OneWare.Studio/Styles/Theme.axaml")
            });

            // Initialize your modules or services manually if required
        }

        protected void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // Modules are handled via Autofac directly now.
            // You can initialize or resolve modules manually if needed.
        }
    }
}
