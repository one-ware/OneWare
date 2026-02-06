using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vhdl.Parsing;
using OneWare.Vhdl.Templates;

namespace OneWare.Vhdl;

public class VhdlModule : OneWareModuleBase
{
    public const string LspName = "RustHDL";
    public const string LspPathSetting = "VhdlModule_RustHdlPath";
    public const string EnableSnippetsSetting = "VhdlModule_EnableSnippets";
    public static readonly string[] SupportedExtensions = [".vhd", ".vhdl"];

    public static readonly Package RustHdlPackage = new()
    {
        Category = "Binaries",
        Id = "rusthdl",
        Type = "NativeTool",
        Name = "RustHDL",
        Description = "Used for VHDL Support",
        License = "MPL 2.0",
        IconUrl =
            "https://raw.githubusercontent.com/VHDL-LS/rust_hdl/cae4e21054386b74937abfba0fa673a659e9a0ce/logo.svg",
        AcceptLicenseBeforeDownload = true,
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/VHDL-LS/rust_hdl"
            }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/VHDL-LS/rust_hdl/master/LICENSE.txt"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = "0.78.1",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.78.1/vhdl_ls-x86_64-pc-windows-msvc.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-pc-windows-msvc", "bin", "vhdl_ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.78.1/vhdl_ls-x86_64-unknown-linux-gnu.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-unknown-linux-gnu", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.78.1/vhdl_ls-aarch64-apple-darwin.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-aarch64-apple-darwin", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion
            {
                Version = "0.82.0",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.82.0/vhdl_ls-x86_64-pc-windows-msvc.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-pc-windows-msvc", "bin", "vhdl_ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.82.0/vhdl_ls-x86_64-unknown-linux-gnu.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-unknown-linux-gnu", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.82.0/vhdl_ls-aarch64-apple-darwin.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-aarch64-apple-darwin", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion
            {
                Version = "0.83.0",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.83.0/vhdl_ls-x86_64-pc-windows-msvc.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-pc-windows-msvc", "bin", "vhdl_ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.83.0/vhdl_ls-x86_64-unknown-linux-gnu.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-unknown-linux-gnu", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.83.0/vhdl_ls-aarch64-apple-darwin.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-aarch64-apple-darwin", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion
            {
                Version = "0.85.0",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.85.0/vhdl_ls-x86_64-pc-windows-msvc.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-pc-windows-msvc", "bin", "vhdl_ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.85.0/vhdl_ls-x86_64-unknown-linux-gnu.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-unknown-linux-gnu", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.85.0/vhdl_ls-aarch64-apple-darwin.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-aarch64-apple-darwin", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion
            {
                Version = "0.86.0",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.86.0/vhdl_ls-x86_64-pc-windows-msvc.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-pc-windows-msvc", "bin", "vhdl_ls.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.86.0/vhdl_ls-x86_64-unknown-linux-gnu.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-x86_64-unknown-linux-gnu", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.86.0/vhdl_ls-aarch64-apple-darwin.zip",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("vhdl_ls-aarch64-apple-darwin", "bin", "vhdl_ls"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    }
                ]
            }
        ]
    };

    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var fpgaService = serviceProvider.Resolve<FpgaService>();

        fpgaService.RegisterLanguage("VHDL", SupportedExtensions);

        var settingsService = serviceProvider.Resolve<ISettingsService>();

        serviceProvider.Resolve<IPackageService>().RegisterPackage(RustHdlPackage);

        settingsService.RegisterSetting("Languages", "VHDL", LspPathSetting,
            new FilePathSetting("RustHDL Path", "", "",
                serviceProvider.Resolve<IPaths>().PackagesDirectory, File.Exists, PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the RustHDL Language Server executable."
            }
        );

        settingsService
            .RegisterSetting("Languages", "VHDL", EnableSnippetsSetting, new CheckBoxSetting("Enable Snippets", true)
            {
                MarkdownDocumentation =
                    "Enable snippets that provide rich completion. These are not smart or context based."
            });

        var nodeProviderComboSetting =
            new ComboBoxSetting("Node Provider", VhdlNodeProvider.NodeProviderKey, [VhdlNodeProvider.NodeProviderKey]);

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);
        serviceProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("vhdl",
            "avares://OneWare.Vhdl/Assets/vhdl.tmLanguage.json", SupportedExtensions);
        serviceProvider.Resolve<ILanguageManager>()
            .RegisterService(typeof(LanguageServiceVhdl), true, SupportedExtensions);

        fpgaService.RegisterNodeProvider<VhdlNodeProvider>();
        fpgaService.RegisterTemplate<VhdlBlinkTemplate>();
        fpgaService.RegisterTemplate<VhdlBlinkSimulationTemplate>();

        // serviceProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x,l) =>
        // {
        //     if (x is [UniversalProjectRoot root])
        //     {
        //         l.Add(new MenuItemViewModel("Refresh_Toml")
        //         {
        //             Header = "Refresh VHDL_LS Toml",
        //             Command = new RelayCommand(() => TomlCreator.RefreshToml(root)),
        //         });
        //     }
        // });
    }
}
