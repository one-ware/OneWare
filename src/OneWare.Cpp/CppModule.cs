using System.IO;
using Autofac;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Cpp;

public class CppModule : Module
{
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
        Versions = [ /* your versions unchanged */ ]
    };

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterBuildCallback(container =>
        {
            var packageService = container.Resolve<IPackageService>();
            var settingsService = container.Resolve<ISettingsService>();
            var errorService = container.Resolve<IErrorService>();
            var languageManager = container.Resolve<ILanguageManager>();
            var paths = container.Resolve<IPaths>();

            packageService.RegisterPackage(ClangdPackage);

            settingsService.RegisterTitledFilePath("Languages", "C++", LspPathSetting,
                "Clangd Path", "Path for clangd executable", "", null,
                paths.NativeToolsDirectory, File.Exists, PlatformHelper.ExeFile);

            errorService.RegisterErrorSource(LspName);

            languageManager.RegisterService(typeof(LanguageServiceCpp), false, ".cpp", ".h", ".c", ".hpp");
        });
    }
}
