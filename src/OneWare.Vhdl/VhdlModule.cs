using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vhdl.Parsing;
using OneWare.Vhdl.Templates;
using Prism.Modularity;

namespace OneWare.Vhdl;

public class VhdlModule 
{
    public const string LspName = "RustHDL";
    public const string LspPathSetting = "VhdlModule_RustHdlPath";
    public const string EnableSnippetsSetting = "VhdlModule_EnableSnippets";

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
            new PackageVersion()
            {
                Version = "0.83.0",
                Targets = [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.83.0/vhdl_ls-x86_64-pc-windows-msvc.zip",
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
                    new PackageTarget()
                    {
                        Target = "osx-arm64",
                        Url = "https://github.com/VHDL-LS/rust_hdl/releases/download/v0.83.0/vhdl_ls-aarch64-apple-darwin.zip",
                        AutoSetting = [
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

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IPackageService>().RegisterPackage(RustHdlPackage);

        containerProvider.Resolve<ISettingsService>().RegisterTitledFilePath("Languages", "VHDL", LspPathSetting,
            "RustHDL Path", "Path for RustHDL executable", "",
            null, containerProvider.Resolve<IPaths>().PackagesDirectory, File.Exists, PlatformHelper.ExeFile);
        
        containerProvider.Resolve<ISettingsService>().RegisterTitled("Languages", "VHDL", EnableSnippetsSetting,
            "Enable Snippets", "Enable snippets that provide rich completion. These are not smart or context based.", true);

        containerProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("vhdl",
            "avares://OneWare.Vhdl/Assets/vhdl.tmLanguage.json", ".vhd", ".vhdl");
        containerProvider.Resolve<ILanguageManager>()
            .RegisterService(typeof(LanguageServiceVhdl), true, ".vhd", ".vhdl");

        containerProvider.Resolve<FpgaService>().RegisterNodeProvider<VhdlNodeProvider>(".vhd", ".vhdl");

        containerProvider.Resolve<FpgaService>().RegisterTemplate<VhdlBlinkTemplate>();
        containerProvider.Resolve<FpgaService>().RegisterTemplate<VhdlBlinkSimulationTemplate>();

        // containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x,l) =>
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