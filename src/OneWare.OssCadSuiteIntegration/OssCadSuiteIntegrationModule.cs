// OneWare.OssCadSuiteIntegration/OssCadSuiteIntegrationModule.cs
using Autofac;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.OssCadSuiteIntegration.Simulators;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Models; // For UniversalFpgaProjectRoot
using OneWare.UniversalFpgaProjectSystem.ViewModels; // For UniversalFpgaProjectPinPlannerViewModel
using OneWare.UniversalFpgaProjectSystem.Fpga; // <--- ADD THIS USING DIRECTIVE FOR Fpga

// ReSharper disable StringLiteralTypo

namespace OneWare.OssCadSuiteIntegration
{
    public class OssCadSuiteIntegrationModule : Module
    {

        public const string OssPathSetting = "OssCadSuite_Path";

        public static readonly Package OssCadPackage = new()
        {
            Category = "Binaries",
            Id = "osscadsuite",
            Type = "NativeTool",
            Name = "OSS CAD Suite",
            Description = "Open Source FPGA Tools",
            License = "ISC",
            IconUrl =
                "https://avatars.githubusercontent.com/u/35169771?s=48&v=4",
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
                            Url =
                                "https://github.com/HendrikMennen/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-windows-x64-20240727.tgz",
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
                            Url =
                                "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-linux-x64-20240727.tgz",
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
                            Url =
                                "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-darwin-x64-20240727.tgz",
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
                            Url =
                                "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2024-07-27/oss-cad-suite-darwin-arm64-20240727.tgz",
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
                            Url =
                                "https://github.com/HendrikMennen/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-windows-x64-20250122.tgz",
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
                            Url =
                                "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-linux-x64-20250122.tgz",
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
                            Url =
                                "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-darwin-x64-20250122.tgz",
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
                            Url =
                                "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2025-01-22/oss-cad-suite-darwin-arm64-20250122.tgz",
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
            // Register services
            builder.RegisterType<YosysService>().AsSelf().SingleInstance();
            builder.RegisterType<GtkWaveService>().AsSelf().SingleInstance();

            // Register ViewModels for factory consumption
            builder.RegisterType<YosysCompileWindowExtensionViewModel>().AsSelf();
            builder.RegisterType<OpenFpgaLoaderWindowExtensionViewModel>().AsSelf();
            builder.RegisterType<YosysCompileSettingsViewModel>().AsSelf();

            // Register Func factories for ViewModels with runtime parameters
            builder.RegisterType<System.Func<UniversalFpgaProjectPinPlannerViewModel, YosysCompileWindowExtensionViewModel>>()
                   .AsSelf().SingleInstance();
            builder.RegisterType<System.Func<UniversalFpgaProjectRoot, OpenFpgaLoaderWindowExtensionViewModel>>()
                   .AsSelf().SingleInstance();
            builder.RegisterType<Func<UniversalFpgaProjectRoot, IFpga, YosysCompileSettingsViewModel>>()
                   .AsSelf().SingleInstance();

            // Register the initializer for this module as a singleton
            builder.RegisterType<OssCadSuiteIntegrationModuleInitializer>().AsSelf().SingleInstance();

            base.Load(builder);
        }
    }
}