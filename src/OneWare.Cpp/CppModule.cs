using Autofac;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Cpp
{
    public class CppModule : Module
    {
        private readonly PlatformHelper _platformHelper;
        public const string LspName = "clangd";
        public const string LspPathSetting = "CppModule_ClangdPath";

        public static readonly Package ClangdPackage = new()
        {
            Category = "Binaries",
            Id = "clangd",
            Type = "NativeTool",
            Name = "clangd",
            Description = "Used for C++ Support",
            License = "Apache 2.0",
            IconUrl = "https://clangd.llvm.org/logo.svg",
            Links =
            [
                new PackageLink
                {
                    Name = "GitHub",
                    Url = "https://github.com/clangd/clangd"
                }
            ],
            Tabs =
            [
                new PackageTab
                {
                    Title = "License",
                    ContentUrl = "https://raw.githubusercontent.com/clangd/clangd/master/LICENSE"
                }
            ],
            Versions =
            [
                new PackageVersion
                {
                    Version = "17.0.3",
                    Targets =
                    [
                        new PackageTarget
                        {
                            Target = "win-x64",
                            Url = "https://github.com/clangd/clangd/releases/download/17.0.3/clangd-windows-17.0.3.zip",
                            AutoSetting =
                            [
                                new PackageAutoSetting
                                {
                                    RelativePath = Path.Combine("clangd_17.0.3", "bin", "clangd.exe"),
                                    SettingKey = LspPathSetting
                                }
                            ]
                        },
                        new PackageTarget
                        {
                            Target = "linux-x64",
                            Url = "https://github.com/clangd/clangd/releases/download/17.0.3/clangd-linux-17.0.3.zip",
                            AutoSetting =
                            [
                                new PackageAutoSetting
                                {
                                    RelativePath = "clangd_17.0.3/bin/clangd",
                                    SettingKey = LspPathSetting
                                }
                            ]
                        },
                        new PackageTarget
                        {
                            Target = "osx-x64",
                            Url = "https://github.com/clangd/clangd/releases/download/17.0.3/clangd-mac-17.0.3.zip",
                            AutoSetting =
                            [
                                new PackageAutoSetting
                                {
                                    RelativePath = "clangd_17.0.3/bin/clangd",
                                    SettingKey = LspPathSetting
                                }
                            ]
                        }
                    ]
                },
                new PackageVersion
                {
                    Version = "18.1.3",
                    Targets =
                    [
                        new PackageTarget
                        {
                            Target = "win-x64",
                            Url = "https://github.com/clangd/clangd/releases/download/18.1.3/clangd-windows-18.1.3.zip",
                            AutoSetting =
                            [
                                new PackageAutoSetting
                                {
                                    RelativePath = Path.Combine("clangd_18.1.3", "bin", "clangd.exe"),
                                    SettingKey = LspPathSetting
                                }
                            ]
                        },
                        new PackageTarget
                        {
                            Target = "linux-x64",
                            Url = "https://github.com/clangd/clangd/releases/download/18.1.3/clangd-linux-18.1.3.zip",
                            AutoSetting =
                            [
                                new PackageAutoSetting
                                {
                                    RelativePath = "clangd_18.1.3/bin/clangd",
                                    SettingKey = LspPathSetting
                                }
                            ]
                        },
                        new PackageTarget
                        {
                            Target = "osx-x64",
                            Url = "https://github.com/clangd/clangd/releases/download/18.1.3/clangd-mac-18.1.3.zip",
                            AutoSetting =
                            [
                                new PackageAutoSetting
                                {
                                    RelativePath = "clangd_18.1.3/bin/clangd",
                                    SettingKey = LspPathSetting
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        protected override void Load(ContainerBuilder builder)
        {
            // No types to register in this module
            base.Load(builder);
        }

        public void OnInitialized(IComponentContext context)
        {
            var packageService = context.Resolve<IPackageService>();
            var settingsService = context.Resolve<ISettingsService>();
            var errorService = context.Resolve<IErrorService>();
            var languageManager = context.Resolve<ILanguageManager>();
            var paths = context.Resolve<IPaths>();

            packageService.RegisterPackage(ClangdPackage);

            settingsService.RegisterTitledFilePath(
                "Languages", "C++", LspPathSetting,
                "Clangd Path", "Path for clangd executable", "",
                null, paths.NativeToolsDirectory, File.Exists, _platformHelper.ExeFile);

            errorService.RegisterErrorSource(LspName);

            languageManager.RegisterService(
                typeof(LanguageServiceCpp), false, ".cpp", ".h", ".c", ".hpp");
        }
    }
}
