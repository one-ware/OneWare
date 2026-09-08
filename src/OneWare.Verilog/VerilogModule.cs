using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Verilog.Parsing;
using OneWare.Verilog.Services;
using OneWare.Verilog.Templates;
using OneWare.Verilog.ViewModels;

namespace OneWare.Verilog;

public class VerilogModule : OneWareModuleBase
{
    public const string LspName = "LazyVerilog";
    public const string LspPathSetting = "VerilogModule_LazyVerilogPath";
    public const string EnableSnippetsSetting = "VerilogModule_EnableSnippets";
    public static readonly string[] VerilogExtensions = [".v", ".vh"];
    public static readonly string[] SystemVerilogExtensions = [".sv", ".svh"];
    public static readonly string[] FileListExtensions = [".f"];
    public static readonly string[] SupportedExtensions = [..VerilogExtensions, ..SystemVerilogExtensions];

    private const string LazyVerilogVersion = "2.0.0";
    private const string ReleaseBaseUrl =
        "https://github.com/lazyverilog/LazyVerilog/releases/download/v2.0.0";

    public static readonly Package LazyVerilogPackage = new()
    {
        Category = "Binaries",
        Id = "lazyverilog",
        Type = "NativeTool",
        Name = "LazyVerilog",
        Description = "SystemVerilog language server for RTL development",
        License = "MIT",
        IconUrl = "https://raw.githubusercontent.com/lazyverilog/LazyVerilog/main/assets/lazyverilog_logo.png",
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/lazyverilog/LazyVerilog"
            }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/lazyverilog/LazyVerilog/main/LICENSE"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = LazyVerilogVersion,
                Targets =
                [
                    CreateTarget("win-x64", "windows-x64.exe"),
                    CreateTarget("linux-x64", "linux-x64-static"),
                    CreateTarget("linux-arm64", "linux-arm64-static"),
                    CreateTarget("osx-x64", "darwin-x64"),
                    CreateTarget("osx-arm64", "darwin-arm64")
                ]
            }
        ]
    };

    private static PackageTarget CreateTarget(string target, string assetPlatform)
    {
        var fileName = $"lazyverilog-lsp-v{LazyVerilogVersion}-{assetPlatform}";
        return new PackageTarget
        {
            Target = target,
            Url = $"{ReleaseBaseUrl}/{fileName}",
            IsArchive = false,
            AutoSetting =
            [
                new PackageAutoSetting
                {
                    RelativePath = fileName,
                    SettingKey = LspPathSetting
                }
            ]
        };
    }

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<LazyVerilogCommandService>();
        services.AddSingleton<VerilogOutlineViewModel>();
        services.AddSingleton<VerilogRtlTreeViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.Resolve<ISettingsService>();
        var fpgaService = serviceProvider.Resolve<FpgaService>();
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var commandService = serviceProvider.Resolve<LazyVerilogCommandService>();

        fpgaService.RegisterLanguage("Verilog", VerilogExtensions);
        fpgaService.RegisterLanguage("SystemVerilog", SystemVerilogExtensions);

        serviceProvider.Resolve<IPackageService>().RegisterPackage(LazyVerilogPackage);

        var pathSetting = new FilePathSetting("LazyVerilog Path", "", null,
            serviceProvider.Resolve<IPaths>().PackagesDirectory,
            File.Exists, PlatformHelper.ExeFile);
        settingsService.RegisterSetting("Languages", "Verilog", LspPathSetting, pathSetting);
        settingsService.RegisterSetting("Languages", "Verilog", EnableSnippetsSetting,
            new CheckBoxSetting("Enable Snippets", true));

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);
        var languageManager = serviceProvider.Resolve<ILanguageManager>();
        languageManager.RegisterTextMateLanguage("verilog",
            "avares://OneWare.Verilog/Assets/verilog.tmLanguage.json", VerilogExtensions);
        languageManager.RegisterTextMateLanguage("systemverilog",
            "avares://OneWare.Verilog/Assets/systemverilog.tmLanguage.json", SystemVerilogExtensions);
        languageManager.RegisterTextMateLanguage("verilog-filelist",
            "avares://OneWare.Verilog/Assets/verilog-filelist.tmLanguage.json", FileListExtensions);
        languageManager.RegisterService(typeof(LanguageServiceVerilog), true, SupportedExtensions);

        dockService.RegisterLayoutExtension<VerilogOutlineViewModel>(DockShowLocation.Right);
        dockService.RegisterLayoutExtension<VerilogRtlTreeViewModel>(DockShowLocation.Right);
        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("VerilogOutline")
            {
                Header = "Verilog Outline",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<VerilogOutlineViewModel>(), DockShowLocation.Right)),
                Icon = new IconModel(VerilogOutlineViewModel.IconKey)
            }, new MenuItemModel("VerilogRtlTree")
            {
                Header = "Verilog RTL Tree",
                Command = new RelayCommand(() =>
                    dockService.Show(serviceProvider.Resolve<VerilogRtlTreeViewModel>(), DockShowLocation.Right)),
                Icon = new IconModel(VerilogRtlTreeViewModel.IconKey)
            });

        var applicationCommands = serviceProvider.Resolve<IApplicationCommandService>();
        RegisterCommand("LazyVerilog: Format Document", commandService.FormatAsync);
        RegisterCommand("LazyVerilog: AutoWire", commandService.AutoWireAsync);
        RegisterCommand("LazyVerilog: AutoFF", commandService.AutoFfAsync);
        RegisterCommand("LazyVerilog: AutoFF All", commandService.AutoFfAllAsync);
        RegisterCommand("LazyVerilog: Lint File", commandService.LintAsync);
        RegisterCommand("LazyVerilog: Lint Project", commandService.LintAllAsync);
        RegisterCommand("LazyVerilog: Inspect Interface", commandService.ShowInterfaceAsync);
        RegisterCommand("LazyVerilog: Connect Hierarchy Ports", commandService.ConnectAsync);
        RegisterCommand("LazyVerilog: Connect Interface Ports", commandService.ConnectInterfacePortsAsync);
        RegisterCommand("LazyVerilog: Disconnect Interface Ports", commandService.DisconnectInterfacePortsAsync);
        RegisterCommand("LazyVerilog: Show RTL Tree", async () =>
        {
            var tree = serviceProvider.Resolve<VerilogRtlTreeViewModel>();
            dockService.Show(tree, DockShowLocation.Right);
            await tree.LoadForwardAsync();
        });
        RegisterCommand("LazyVerilog: Show Reverse RTL Tree", async () =>
        {
            var tree = serviceProvider.Resolve<VerilogRtlTreeViewModel>();
            dockService.Show(tree, DockShowLocation.Right);
            await tree.LoadReverseAsync();
        });

        void RegisterCommand(string name, Func<Task> action)
        {
            applicationCommands.RegisterCommand(new SimpleApplicationCommand(name, () => _ = action(),
                () => commandService.CanExecute));
        }

        fpgaService.RegisterTemplate<VerilogBlinkTemplate>();
        fpgaService.RegisterTemplate<VerilogBlinkSimulationTemplate>();

        fpgaService.RegisterNodeProvider<VerilogNodeProvider>();
    }
}
