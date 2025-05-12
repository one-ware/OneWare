using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Verilog.Parsing;
using OneWare.Verilog.Templates;
using System.IO;
using Autofac;

namespace OneWare.Verilog;

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
            new PackageLink { Name = "GitHub", Url = "https://github.com/chipsalliance/verible" }
        ],
        Tabs =
        [
            new PackageTab { Title = "License", ContentUrl = "https://raw.githubusercontent.com/chipsalliance/verible/master/LICENSE" }
        ],
        Versions = [ /* ... Keep the existing PackageVersion list as is ... */ ]
    };

    protected override void Load(ContainerBuilder builder)
    {
        // Register any Verilog-specific services here if needed
    }

    public static void Initialize(ILifetimeScope scope)
    {
        var packageService = scope.Resolve<IPackageService>();
        var settingsService = scope.Resolve<ISettingsService>();
        var paths = scope.Resolve<IPaths>();
        var errorService = scope.Resolve<IErrorService>();
        var languageManager = scope.Resolve<ILanguageManager>();
        var fpgaService = scope.Resolve<FpgaService>();

        packageService.RegisterPackage(VeriblePackage);

        settingsService.RegisterTitledFilePath("Languages", "Verilog", LspPathSetting,
            "Verible Path", "Path for Verible executable", "",
            null, paths.PackagesDirectory, File.Exists, PlatformHelper.ExeFile);

        settingsService.RegisterTitled("Languages", "Verilog", EnableSnippetsSetting,
            "Enable Snippets", "Enable snippets that provide rich completion. These are not smart or context based.", true);

        errorService.RegisterErrorSource(LspName);

        languageManager.RegisterTextMateLanguage("verilog",
            "avares://OneWare.Verilog/Assets/verilog.tmLanguage.json", ".v", ".sv");

        languageManager.RegisterService(typeof(LanguageServiceVerilog), true, ".v", ".sv");

        fpgaService.RegisterNodeProvider<VerilogNodeProvider>(".v", ".sv");
        fpgaService.RegisterTemplate<VerilogBlinkTemplate>();
        fpgaService.RegisterTemplate<VerilogBlinkSimulationTemplate>();
    }
}
