// OneWare.Vhdl/VhdlModule.cs
using Autofac; // Changed from Prism.Modularity
using OneWare.Essentials.Models; // Ensure this is present for Package and related types
using OneWare.Essentials.PackageManager;
using System.IO; // Ensure this is present for Path.Combine

namespace OneWare.Vhdl;

// Now inherits from Autofac.Module
public class VhdlModule : Module
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

    // This method is for registering types with Autofac
    protected override void Load(ContainerBuilder builder)
    {
        // Register the LanguageServiceVhdl if it's a service meant to be resolved by the DI container.
        // Assuming it has a public constructor and its dependencies are also registered.
        builder.RegisterType<LanguageServiceVhdl>().AsSelf().SingleInstance(); // Or whatever lifecycle is appropriate

        // Register the initializer as a singleton so it can be resolved and its Initialize method called
        builder.RegisterType<VhdlModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder); // Call the base Autofac.Module Load method
    }

    // The OnInitialized method is removed from here as it's part of Prism's IModule,
    // and its logic is now handled by VhdlModuleInitializer.
}