using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
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
                Icon = new IconModel("UniversalProject")
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
                Icon = new IconModel("UniversalProject")
            });

        settingsService.Register("UniversalFpgaProjectSystem_LongTermProgramming", false);

        serviceProvider.Resolve<IProjectManagerService>()
            .RegisterProjectManager(UniversalFpgaProjectRoot.ProjectType, manager);

        serviceProvider.Resolve<ILanguageManager>()
            .RegisterLanguageExtensionLink(UniversalFpgaProjectRoot.ProjectFileExtension, ".json");

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/New",
            new MenuItemModel("FpgaProject")
            {
                Header = "FPGA Project",
                Command = new AsyncRelayCommand(() => _ = manager.NewProjectDialogAsync()),
                Priority = 1,
                Icon = new IconModel("UniversalProject")
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("FpgaProject")
            {
                Header = "FPGA Project",
                Command = new AsyncRelayCommand(() => serviceProvider.Resolve<IProjectExplorerService>()
                    .LoadProjectFileDialogAsync(manager,
                        new FilePickerFileType(
                            $"Project (*{UniversalFpgaProjectRoot.ProjectFileExtension})")
                        {
                            Patterns = [$"*{UniversalFpgaProjectRoot.ProjectFileExtension}"]
                        })),
                Icon = new IconModel("UniversalProject")
            });

        var toolBarViewModel = serviceProvider.Resolve<UniversalFpgaProjectToolBarViewModel>();

        windowService.RegisterMenuItem("MainWindow_MainMenu",
            new MenuItemModel("FPGA")
            {
                Header = "FPGA",
                Priority = 200
            });

        windowService.RegisterMenuItem("MainWindow_MainMenu/FPGA", new MenuItemModel("Download")
        {
            Header = "Download",
            Command = new AsyncRelayCommand(() => toolBarViewModel.DownloadAsync()),
            Icon = new IconModel("VsImageLib.Download16X")
        }, new MenuItemModel("Compile")
        {
            Header = "Compile",
            Command = new AsyncRelayCommand(() => toolBarViewModel.CompileAsync()),
            Icon = new IconModel("CreateIcon")
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

        var fpgaService = serviceProvider.Resolve<FpgaService>();
        var projectSettingsService = serviceProvider.Resolve<IProjectSettingsService>();
        
        fpgaService.RegisterProjectPropertyMigration("VHDL_Standard", "vhdlStandard");
        fpgaService.RegisterProjectPropertyMigration("Toolchain", "toolchain", NormalizeToolchain);
        fpgaService.RegisterProjectPropertyMigration("Loader", "loader", NormalizeLoader);
        fpgaService.RegisterProjectPropertyMigration("fpga", "board"); // legacy key renamed to "board"
        fpgaService.RegisterProjectPropertyMigration("Fpga", "board"); // PascalCase variant also migrated

        RegisterProjectSettings(projectSettingsService, fpgaService);
    }

    private static void RegisterProjectSettings(IProjectSettingsService svc, FpgaService fpgaService)
    {
        svc.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("topEntity")
                .WithCategory("Project")
                .WithDisplayOrder(60)
                .WithFactory(async root =>
                {
                    if (root is not UniversalFpgaProjectRoot fpgaRoot)
                        return new AdvancedComboBoxSearchSetting("Top Entity", "", []);

                    var allEntities = await fpgaService.GetAllTopEntitiesAsync(fpgaRoot);
                    var options = allEntities.Select(x => new AdvancedComboBoxOption
                    {
                        Title = $"{x.TopEntity} ({x.File.RelativePath})",
                        Value = x.TopEntity
                    }).ToArray();
                    return new AdvancedComboBoxSearchSetting("Top Entity", fpgaRoot.TopEntity ?? "", options)
                    {
                        MarkdownDocumentation =
                            "The top-level entity or module used for synthesis and pin planning.\n\n" +
                            "This name must match the entity/module declaration in your HDL source files."
                    };
                })
                .Build()
        );

        svc.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("toolchain")
                .WithCategory("Project")
                .WithDisplayOrder(70)
                .WithFactory(root =>
                {
                    var options = fpgaService.Toolchains.Select(tc => tc.Id).ToArray<object>();
                    var current = root is UniversalFpgaProjectRoot r ? r.Toolchain ?? "" : "";
                    return Task.FromResult<TitledSetting>(new ComboBoxSetting("Toolchain", current, options)
                    {
                        MarkdownDocumentation =
                            "The synthesis and place-and-route toolchain used to compile this project.\n\n" +
                            "The toolchain determines the full compile pipeline (synthesis, fit, assemble)."
                    });
                })
                .Build()
        );

        svc.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("loader")
                .WithCategory("Project")
                .WithDisplayOrder(80)
                .WithFactory(root =>
                {
                    var options = fpgaService.Loaders.Select(l => l.Id).ToArray<object>();
                    var current = root is UniversalFpgaProjectRoot r ? r.Loader ?? "" : "";
                    return Task.FromResult<TitledSetting>(new ComboBoxSetting("Loader", current, options)
                    {
                        MarkdownDocumentation =
                            "The programming tool used to download the bitstream to the FPGA.\n\n" +
                            "Examples: `openFPGALoader`, `iceprog`."
                    });
                })
                .Build()
        );

        svc.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("vhdlStandard")
                .WithCategory("Project")
                .WithDisplayOrder(90)
                .WithFactory(root =>
                {
                    var current = root is UniversalFpgaProjectRoot r
                        ? r.Properties.GetString("vhdlStandard") ?? "" : "";
                    return Task.FromResult<TitledSetting>(
                        new ComboBoxSetting("VHDL Standard", current,
                            ["87", "93", "93c", "00", "02", "08", "19"])
                        {
                            MarkdownDocumentation =
                                "The VHDL language standard version used when analysing and simulating VHDL files.\n\n" +
                                "Common choices:\n- `08` — VHDL-2008 (recommended)\n- `93` — VHDL-93\n- `02` — VHDL-2002"
                        });
                })
                .WithActivation(file =>
                {
                    if (file is UniversalFpgaProjectRoot root)
                        return root.GetFiles().Any(f => Path.GetExtension(f) is ".vhd" or ".vhdl");
                    return false;
                })
                .Build()
        );

        svc.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("include")
                .WithCategory("Files")
                .WithDisplayOrder(100)
                .WithFactory(root =>
                {
                    var items = root is UniversalFpgaProjectRoot r
                        ? r.Properties.GetStringArray("include")?.ToArray() ?? []
                        : Array.Empty<string>();
                    return Task.FromResult<TitledSetting>(new ListBoxSetting("Files to Include", items)
                    {
                        MarkdownDocumentation =
                            "Glob patterns or relative paths that are **explicitly included** in the project file set.\n\n" +
                            "Leave empty to include all files in the project directory."
                    });
                })
                .Build()
        );

        svc.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("exclude")
                .WithCategory("Files")
                .WithDisplayOrder(110)
                .WithFactory(root =>
                {
                    var items = root is UniversalFpgaProjectRoot r
                        ? r.Properties.GetStringArray("exclude")?.ToArray() ?? []
                        : Array.Empty<string>();
                    return Task.FromResult<TitledSetting>(new ListBoxSetting("Files to Exclude", items)
                    {
                        MarkdownDocumentation =
                            "Glob patterns or relative paths that are **excluded** from the project file set.\n\n" +
                            "Excluded files are hidden from the project explorer and not passed to any tool."
                    });
                })
                .Build()
        );
        
        svc.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("compileExcluded")
                .WithCategory("Files")
                .WithDisplayOrder(120)
                .WithFactory(root =>
                {
                    var items = root is UniversalFpgaProjectRoot r
                        ? r.Properties.GetStringArray("compileExcluded")?.ToArray() ?? []
                        : Array.Empty<string>();
                    return Task.FromResult<TitledSetting>(new ListBoxSetting("Compile Excluded", items)
                    {
                        MarkdownDocumentation =
                            "Relative paths of source files that are **excluded from compilation**.\n\n" +
                            "The files remain visible in the project explorer but are not passed to the synthesiser."
                    });
                })
                .Build()
        );
    }

    private static JsonNode? NormalizeToolchain(JsonNode? node)
    {
        if (node == null)
            return null;

        var value = node.ToString();
        if (string.Equals(value, "Yosys", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create("yosys");

        return node;
    }

    private static JsonNode? NormalizeLoader(JsonNode? node)
    {
        if (node == null)
            return null;

        var value = node.ToString();
        if (string.Equals(value, "OpenFpgaLoader", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create("openFpgaLoader");

        return node;
    }
}
