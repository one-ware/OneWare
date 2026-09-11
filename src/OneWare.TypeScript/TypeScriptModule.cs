using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.TypeScript;

public class TypeScriptModule : OneWareModuleBase
{
    public const string LspName = "tsc";
    public const string LspPathSetting = "TypeScriptModule_TscPath";

    /// <summary>
    ///     Extensions handled by the language server. .mts/.cts are resolved through extension links.
    /// </summary>
    public static readonly string[] SupportedExtensions =
        [".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs"];

    private const string TypeScriptVersion = "7.0.2";

    public static readonly Package TypeScriptPackage = new()
    {
        Category = "Binaries",
        Id = "typescript",
        Type = "NativeTool",
        Name = "TypeScript (native tsc)",
        Description = "Used for JavaScript and TypeScript Support",
        License = "Apache 2.0",
        IconUrl = "https://raw.githubusercontent.com/lobehub/lobe-icons/refs/heads/master/packages/static-png/dark/typescript.png",
        Links =
        [
            new PackageLink
            {
                Name = "GitHub",
                Url = "https://github.com/microsoft/TypeScript"
            }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/microsoft/TypeScript/main/LICENSE.txt"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = TypeScriptVersion,
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/typescript-win32-x64/-/typescript-win32-x64-{TypeScriptVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("package", "lib", "tsc.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "win-arm64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/typescript-win32-arm64/-/typescript-win32-arm64-{TypeScriptVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = Path.Combine("package", "lib", "tsc.exe"),
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/typescript-linux-x64/-/typescript-linux-x64-{TypeScriptVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsc",
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "linux-arm64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/typescript-linux-arm64/-/typescript-linux-arm64-{TypeScriptVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsc",
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/typescript-darwin-x64/-/typescript-darwin-x64-{TypeScriptVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsc",
                                SettingKey = LspPathSetting
                            }
                        ]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url =
                            $"https://registry.npmjs.org/@typescript/typescript-darwin-arm64/-/typescript-darwin-arm64-{TypeScriptVersion}.tgz",
                        AutoSetting =
                        [
                            new PackageAutoSetting
                            {
                                RelativePath = "package/lib/tsc",
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
        serviceProvider.Resolve<IPackageService>().RegisterPackage(TypeScriptPackage);

        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Languages", "TypeScript", LspPathSetting,
            new FilePathSetting("tsc Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for the native tsc executable"
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
