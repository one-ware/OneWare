// OneWare.Verilog/VerilogModuleInitializer.cs
using System;
using System.IO;
using System.Reactive.Linq; // For .Subscribe()
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Verilog.Parsing;
using OneWare.Verilog.Templates;
using OneWare.Essentials.Models; // For Package, PackageLink, PackageTab, PackageVersion, PackageTarget, PackageAutoSetting

namespace OneWare.Verilog;

public class VerilogModuleInitializer
{
    private readonly IPackageService _packageService;
    private readonly ISettingsService _settingsService;
    private readonly IErrorService _errorService;
    private readonly ILanguageManager _languageManager;
    private readonly FpgaService _fpgaService;
    private readonly IPaths _paths;
    private readonly ILogger _logger; // Assuming ILogger is available and registered
    private readonly PlatformHelper _platformHelper; // Injected PlatformHelper

    public VerilogModuleInitializer(
        IPackageService packageService,
        ISettingsService settingsService,
        IErrorService errorService,
        ILanguageManager languageManager,
        FpgaService fpgaService,
        IPaths paths,
        ILogger logger,
        PlatformHelper platformHelper)
    {
        _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _errorService = errorService ?? throw new ArgumentNullException(nameof(errorService));
        _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
        _fpgaService = fpgaService ?? throw new ArgumentNullException(nameof(fpgaService));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _platformHelper = platformHelper ?? throw new ArgumentNullException(nameof(platformHelper));
    }

    public void Initialize()
    {
        _packageService.RegisterPackage(VerilogModule.VeriblePackage);

        // Path validation using a lambda for File.Exists
        _settingsService.RegisterTitledFilePath("Languages", "Verilog", VerilogModule.LspPathSetting,
            "Verible Path", "Path for Verible executable", "",
            null, _paths.PackagesDirectory, path => File.Exists(path), _platformHelper.ExeFile);

        _settingsService.RegisterTitled("Languages", "Verilog", VerilogModule.EnableSnippetsSetting,
            "Enable Snippets", "Enable snippets that provide rich completion. These are not smart or context based.", true);

        // Subscribe to LSP path changes to log if the path becomes invalid
        _settingsService.GetSettingObservable<string>(VerilogModule.LspPathSetting)
            .Subscribe(path =>
            {
                if (!string.IsNullOrEmpty(path) && !File.Exists(path))
                {
                    _logger.Warning($"Verible path invalid: {path}");
                }
            });


        _errorService.RegisterErrorSource(VerilogModule.LspName);

        _languageManager.RegisterTextMateLanguage("verilog",
            "avares://OneWare.Verilog/Assets/verilog.tmLanguage.json", ".v", ".sv");
        _languageManager.RegisterService(typeof(LanguageServiceVerilog), true, ".v", ".sv");

        _fpgaService.RegisterNodeProvider<VerilogNodeProvider>(".v", ".sv");
        _fpgaService.RegisterTemplate<VerilogBlinkTemplate>();
        _fpgaService.RegisterTemplate<VerilogBlinkSimulationTemplate>();
    }
}