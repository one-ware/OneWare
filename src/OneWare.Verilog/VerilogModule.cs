using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Verilog.Parsing;
using OneWare.Verilog.Templates;
using OneWare.Core.Adapters;
using Prism.Modularity;

namespace OneWare.Verilog;

public class VerilogModule : IModule
{
    public const string LspName = "Verible";
    public const string LspPathSetting = "VerilogModule_VeriblePath";
    public const string EnableSnippetsSetting = "VerilogModule_EnableSnippets";

    public static readonly Package VeriblePackage = new()
    {
        Category = "Binaries",
        Id = "verible",
        Type = "NativeTool",
        Name = "Verible",
        Description = "Used for Verilog/SystemVerilog Support",
        License = "Apache 2.0",
        IconUrl = "https://raw.githubusercontent.com/chipsalliance/verible/master/img/verible-logo-headline.png",
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/chipsalliance/verible"
            }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/chipsalliance/verible/master/LICENSE"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = "0.0.3582",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = "https://github.com/chipsalliance/verible/releases/download/v0.0-3582-g25611a89/verible-v0.0-3582-g25611a89-win64.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3582-g25611a89-win64",
                                    "verible-verilog-ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = "https://github.com/chipsalliance/verible/releases/download/v0.0-3582-g25611a89/verible-v0.0-3582-g25611a89-linux-static-x86_64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3582-g25611a89", "bin", "verible-verilog-ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url = "https://github.com/chipsalliance/verible/releases/download/v0.0-3582-g25611a89/verible-v0.0-3582-g25611a89-macOS.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3582-g25611a89-macOS", "bin",
                                    "verible-verilog-ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion
            {
                Version = "0.0.3716",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = 
                            "https://github.com/chipsalliance/verible/releases/download/v0.0-3716-g914652db/verible-v0.0-3716-g914652db-win64.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3716-g914652db-win64",
                                    "verible-verilog-ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = 
                            "https://github.com/chipsalliance/verible/releases/download/v0.0-3716-g914652db/verible-v0.0-3716-g914652db-linux-static-x86_64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3716-g914652db", "bin", "verible-verilog-ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url = "https://github.com/chipsalliance/verible/releases/download/v0.0-3716-g914652db/verible-v0.0-3716-g914652db-macOS.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3716-g914652db-macOS", "bin",
                                    "verible-verilog-ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion()
            {
                Version = "0.0.3836",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = 
                            "https://github.com/chipsalliance/verible/releases/download/v0.0-3836-g86ee9bab/verible-v0.0-3836-g86ee9bab-win64.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3836-g86ee9bab-win64",
                                    "verible-verilog-ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = 
                            "https://github.com/chipsalliance/verible/releases/download/v0.0-3836-g86ee9bab/verible-v0.0-3836-g86ee9bab-linux-static-x86_64.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3836-g86ee9bab", "bin", "verible-verilog-ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url = "https://github.com/chipsalliance/verible/releases/download/v0.0-3836-g86ee9bab/verible-v0.0-3836-g86ee9bab-macOS.tar.gz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("verible-v0.0-3836-g86ee9bab-macOS", "bin",
                                    "verible-verilog-ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            }
        ]
    };

    public void RegisterTypes(IContainerAdapter containerAdapter)
    {
    }

    public void OnInitialized(IContainerAdapter containerAdapter)
    {
        containerAdapter.Resolve<IPackageService>().RegisterPackage(VeriblePackage);

        var settingsService = containerAdapter.Resolve<ISettingsService>();
        settingsService.RegisterTitledFilePath("Languages", "Verilog", LspPathSetting,
            "Verible Path", "Path for Verible executable", "",
            null, containerAdapter.Resolve<IPaths>().PackagesDirectory, File.Exists, PlatformHelper.ExeFile);
        
        settingsService.RegisterTitled("Languages", "Verilog", EnableSnippetsSetting,
            "Enable Snippets", "Enable snippets that provide rich completion. These are not smart or context based.", true);

        containerAdapter.Resolve<IErrorService>().RegisterErrorSource(LspName);
        
        var languageManager = containerAdapter.Resolve<ILanguageManager>();
        languageManager.RegisterTextMateLanguage("verilog",
            "avares://OneWare.Verilog/Assets/verilog.tmLanguage.json", ".v", ".sv");
        languageManager.RegisterService(typeof(LanguageServiceVerilog), true, ".v", ".sv");

        var fpgaService = containerAdapter.Resolve<FpgaService>();
        fpgaService.RegisterNodeProvider<VerilogNodeProvider>(".v", ".sv");
        fpgaService.RegisterTemplate<VerilogBlinkTemplate>();
        fpgaService.RegisterTemplate<VerilogBlinkSimulationTemplate>();
    }
}