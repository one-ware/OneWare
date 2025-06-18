// OneWare.OssCadSuiteIntegration/OssCadSuiteIntegrationModule.cs
using Autofac; // Essential for Autofac.Module
using OneWare.Essentials.PackageManager; // For Package
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.OssCadSuiteIntegration.Simulators;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Yosys;

namespace OneWare.OssCadSuiteIntegration;

public class OssCadSuiteIntegrationModule : Module // Inherit from Autofac.Module
{
    // Constants and static package definitions remain here as they are data, not logic.
    public const string OssPathSetting = "OssCadSuite_Path";

    public static readonly Package OssCadPackage = new()
    {
        Category = "Binaries",
        Id = "osscadsuite",
        Type = "NativeTool",
        Name = "OSS CAD Suite",
        Description = "Open Source FPGA Tools",
        License = "ISC",
        IconUrl = "https://avatars.githubusercontent.com/u/35169771?s=48&v=4",
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/YosysHQ/oss-cad-suite-build"
            }
        ],
        Tabs =
        [
            new PackageTab()
            {
                Title = "Readme",
                ContentUrl = "https://raw.githubusercontent.com/HendrikMennen/oss-cad-suite-build/main/README.md"
            },
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/YosysHQ/oss-cad-suite-build/main/COPYING"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = "2024.07.27",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = "https://github.com/HendrikMennen/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-windows-x64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-linux-x64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url = "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-darwin-x64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url = "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-darwin-arm64-20240727.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    }
                ]
            },
            new PackageVersion
            {
                Version = "2025.01.22",
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = "https://github.com/HendrikMennen/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-windows-x64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-linux-x64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url = "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-darwin-x64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url = "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-darwin-arm64-20250122.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "oss-cad-suite",
                                SettingKey = OssPathSetting
                            }
                        ]
                    }
                ]
            },
        ]
    };

    protected override void Load(ContainerBuilder builder)
    {
        // Register core services for this module
        builder.RegisterType<YosysService>().AsSelf().SingleInstance();
        builder.RegisterType<GtkWaveService>().AsSelf().SingleInstance();
        builder.RegisterType<OpenFpgaLoader>().AsSelf().As<IFpgaLoader>().SingleInstance();
        builder.RegisterType<IcarusVerilogSimulator>().AsSelf().As<IFpgaSimulator>().SingleInstance();
        builder.RegisterType<YosysToolchain>().AsSelf().As<IFpgaToolchain>().SingleInstance();

        // Register ViewModels that will be resolved contextually
        builder.RegisterType<YosysCompileWindowExtensionViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<OpenFpgaLoaderWindowExtensionViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<YosysCompileSettingsViewModel>().AsSelf().InstancePerDependency();

        // Register the initializer for this module
        builder.RegisterType<OssCadSuiteIntegrationModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder);
    }
}