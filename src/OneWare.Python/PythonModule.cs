using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Python;

public class PythonModule : OneWareModuleBase
{
    public const string LspName = "pyrefly";
    public const string LspPathSetting = "PythonModule_PyreflyPath";
    public const string PyreflyVersion = "1.3.0";

    public static readonly string[] SupportedExtensions = [".py", ".pyi"];

    public static readonly Package PyreflyPackage = new()
    {
        Category = "Binaries",
        Id = LspName,
        Type = "NativeTool",
        Name = "Pyrefly",
        Description = "Python language support",
        License = "MIT",
        Links = [new PackageLink { Name = "GitHub", Url = "https://github.com/facebook/pyrefly" }],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = $"https://raw.githubusercontent.com/facebook/pyrefly/{PyreflyVersion}/LICENSE"
            }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = PyreflyVersion,
                Targets =
                [
                    CreateTarget("win-x64", "windows-x86_64.zip", "pyrefly.exe"),
                    CreateTarget("win-arm64", "windows-arm64.zip", "pyrefly.exe"),
                    CreateTarget("linux-x64", "linux-x86_64-musl.tar.gz", "pyrefly"),
                    CreateTarget("linux-arm64", "linux-arm64-musl.tar.gz", "pyrefly"),
                    CreateTarget("osx-x64", "macos-x86_64.tar.gz", "pyrefly"),
                    CreateTarget("osx-arm64", "macos-arm64.tar.gz", "pyrefly")
                ]
            }
        ]
    };

    private static PackageTarget CreateTarget(string target, string archive, string executable) => new()
    {
        Target = target,
        Url = $"https://github.com/facebook/pyrefly/releases/download/{PyreflyVersion}/pyrefly-{archive}",
        AutoSetting = [new PackageAutoSetting { RelativePath = executable, SettingKey = LspPathSetting }]
    };

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<PythonInterpreterService>();
        services.AddSingleton<PythonInterpreterPickerService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IPackageService>().RegisterPackage(PyreflyPackage);

        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Languages", "Python", LspPathSetting,
            new FilePathSetting("Pyrefly Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for the Pyrefly executable. Leave empty to use automatic installation."
            });

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);
        serviceProvider.Resolve<PythonInterpreterService>().Initialize();
        serviceProvider.Resolve<PythonInterpreterPickerService>().Initialize();

        var languageManager = serviceProvider.Resolve<ILanguageManager>();
        languageManager.RegisterLanguageExtensionLink(".pyi", ".py");
        languageManager.RegisterService(typeof(LanguageServicePython), true, SupportedExtensions);
    }
}