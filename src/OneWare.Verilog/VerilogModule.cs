// OneWare.Verilog/VerilogModule.cs
using Autofac; // Changed from Prism.Modularity
using OneWare.Essentials.Models; // Ensure this is present for Package and related types
using OneWare.Essentials.PackageManager;
using System.IO; // Ensure this is present for Path.Combine

namespace OneWare.Verilog;

// Now inherits from Autofac.Module
public class VerilogModule : Module
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

    // This method is for registering types with Autofac
    protected override void Load(ContainerBuilder builder)
    {
        // Register the LanguageServiceVerilog if it's a service meant to be resolved by the DI container.
        builder.RegisterType<LanguageServiceVerilog>().AsSelf().SingleInstance(); // Or whatever lifecycle is appropriate

        // Register the initializer as a singleton so it can be resolved and its Initialize method called
        builder.RegisterType<VerilogModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder); // Call the base Autofac.Module Load method
    }

    // The RegisterTypes and OnInitialized methods (from Prism's IModule) are removed
    // as they are replaced by Autofac's Load method and a separate initializer class.
}