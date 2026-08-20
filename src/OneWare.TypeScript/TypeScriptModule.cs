using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.TypeScript;

public class TypeScriptModule : OneWareModuleBase
{
    public const string LspName = "tsgo";
    public const string LspPathSetting = "TypeScriptModule_TsgoPath";

    /// <summary>
    ///     Extensions handled by the language server. .mts/.cts are resolved through extension links.
    /// </summary>
    public static readonly string[] SupportedExtensions =
        [".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs"];

    private const string TsgoVersion = "7.0.0-dev.20260707.2";

    public static readonly Package TsgoPackage = new()
    {
        Category = "Binaries",
        Id = "tsgo",
        Type = "NativeTool",
        Name = "TypeScript Native Preview (tsgo)",
        Description = "Used for JavaScript and TypeScript Support",
        License = "Apache 2.0",
        IconUrl = "https://raw.githubusercontent.com/lobehub/lobe-icons/refs/heads/master/packages/static-png/dark/typescript.png",
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/microsoft/typescript-go"
            }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/microsoft/typescript-go/main/LICENSE"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = TsgoVersion,
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/native-preview-win32-x64/-/native-preview-win32-x64-{TsgoVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("package", "lib", "tsgo.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "win-arm64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/native-preview-win32-arm64/-/native-preview-win32-arm64-{TsgoVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("package", "lib", "tsgo.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/native-preview-linux-x64/-/native-preview-linux-x64-{TsgoVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsgo",
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-arm64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/native-preview-linux-arm64/-/native-preview-linux-arm64-{TsgoVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsgo",
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/native-preview-darwin-x64/-/native-preview-darwin-x64-{TsgoVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsgo",
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/native-preview-darwin-arm64/-/native-preview-darwin-arm64-{TsgoVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsgo",
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
        serviceProvider.Resolve<IPackageService>().RegisterPackage(TsgoPackage);

        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Languages", "TypeScript", LspPathSetting,
            new FilePathSetting("tsgo Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for the tsgo executable"
            });

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);

        var languageManager = serviceProvider.Resolve<ILanguageManager>();

        //TextMate does not know about the module/commonjs specific TypeScript extensions
        languageManager.RegisterLanguageExtensionLink(".mts", ".ts");
        languageManager.RegisterLanguageExtensionLink(".cts", ".ts");

        languageManager.RegisterService(typeof(LanguageServiceTypeScript), true, SupportedExtensions);

        var fileIconService = serviceProvider.Resolve<IFileIconService>();
        fileIconService.RegisterFileIcon("SimpleIcons.TypeScript", ".ts", ".tsx", ".mts", ".cts");
        fileIconService.RegisterFileIcon("Ionicons.LogoJavascript", ".jsx", ".mjs", ".cjs");
    }
}
