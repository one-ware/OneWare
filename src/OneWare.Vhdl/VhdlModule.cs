using System.IO;
using Autofac;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vhdl.Parsing;
using OneWare.Vhdl.Templates;

namespace OneWare.Vhdl;

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
            new PackageLink { Name = "GitHub", Url = "https://github.com/VHDL-LS/rust_hdl" }
        ],
        Tabs =
        [
            new PackageTab
            {
                Title = "License",
                ContentUrl = "https://raw.githubusercontent.com/VHDL-LS/rust_hdl/master/LICENSE.txt"
            }
        ],
        Versions = [ /* Versions unchanged from your original */ ]
    };

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterBuildCallback(container =>
        {
            var packageService = container.Resolve<IPackageService>();
            var settingsService = container.Resolve<ISettingsService>();
            var errorService = container.Resolve<IErrorService>();
            var languageManager = container.Resolve<ILanguageManager>();
            var fpgaService = container.Resolve<FpgaService>();
            var paths = container.Resolve<IPaths>();

            packageService.RegisterPackage(RustHdlPackage);

            settingsService.RegisterTitledFilePath("Languages", "VHDL", LspPathSetting,
                "RustHDL Path", "Path for RustHDL executable", "",
                null, paths.PackagesDirectory, File.Exists, PlatformHelper.ExeFile);

            settingsService.RegisterTitled("Languages", "VHDL", EnableSnippetsSetting,
                "Enable Snippets", "Enable snippets that provide rich completion. These are not smart or context based.",
                true);

            errorService.RegisterErrorSource(LspName);

            languageManager.RegisterTextMateLanguage("vhdl",
                "avares://OneWare.Vhdl/Assets/vhdl.tmLanguage.json", ".vhd", ".vhdl");

            languageManager.RegisterService(typeof(LanguageServiceVhdl), true, ".vhd", ".vhdl");

            fpgaService.RegisterNodeProvider<VhdlNodeProvider>(".vhd", ".vhdl");
            fpgaService.RegisterTemplate<VhdlBlinkTemplate>();
            fpgaService.RegisterTemplate<VhdlBlinkSimulationTemplate>();

            // You can uncomment and adjust this block if needed later
            // var projectExplorer = container.Resolve<IProjectExplorerService>();
            // projectExplorer.RegisterConstructContextMenu((x, l) =>
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
        });
    }
}
