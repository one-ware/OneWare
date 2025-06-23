// OneWare.Vhdl/VhdlModuleInitializer.cs
using System;
using System.IO;
using System.Reactive.Linq; // For .Subscribe()
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vhdl.Parsing;
using OneWare.Vhdl.Templates;
using OneWare.Essentials.Models; // For Package, PackageLink, PackageTab, PackageVersion, PackageTarget, PackageAutoSetting

namespace OneWare.Vhdl;

public class VhdlModuleInitializer
{
    private readonly IPackageService _packageService;
    private readonly ISettingsService _settingsService;
    private readonly IErrorService _errorService;
    private readonly ILanguageManager _languageManager;
    private readonly FpgaService _fpgaService;
    private readonly IPaths _paths;
    private readonly ILogger _logger; // Assuming ILogger is available and registered
    private readonly PlatformHelper _platformHelper; // Injected PlatformHelper

    public VhdlModuleInitializer(
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
        _packageService.RegisterPackage(VhdlModule.RustHdlPackage);

        // Path validation can use a lambda if File.Exists is sufficient, or a private method for more complex logic.
        // Using PlatformHelper.ExeFile is a static property, it doesn't strictly need PlatformHelper injected for this specific use.
        // However, injecting PlatformHelper is good practice if it has other instance methods.
        _settingsService.RegisterTitledFilePath("Languages", "VHDL", VhdlModule.LspPathSetting,
            "RustHDL Path", "Path for RustHDL executable", "",
            null, _paths.PackagesDirectory, IsRustHdlPathValid, _platformHelper.ExeFile); // Use instance method for validation

        _settingsService.RegisterTitled("Languages", "VHDL", VhdlModule.EnableSnippetsSetting,
            "Enable Snippets", "Enable snippets that provide rich completion. These are not smart or context based.", true);

        // Subscribe to LSP path changes to update EnvironmentService if needed
        _settingsService.GetSettingObservable<string>(VhdlModule.LspPathSetting)
            .Subscribe(path =>
            {
                if (!string.IsNullOrEmpty(path) && !IsRustHdlPathValid(path))
                {
                    _logger.Warning($"RustHDL path invalid: {path}");
                }
                // You might want to update environment variables here if RustHDL requires it,
                // similar to what was done in OssCadSuiteIntegrationModuleInitializer.
            });


        _errorService.RegisterErrorSource(VhdlModule.LspName);
        _languageManager.RegisterTextMateLanguage("vhdl",
            "avares://OneWare.Vhdl/Assets/vhdl.tmLanguage.json", ".vhd", ".vhdl");
        _languageManager
            .RegisterService(typeof(LanguageServiceVhdl), true, ".vhd", ".vhdl");

        _fpgaService.RegisterNodeProvider<VhdlNodeProvider>(".vhd", ".vhdl");

        _fpgaService.RegisterTemplate<VhdlBlinkTemplate>();
        _fpgaService.RegisterTemplate<VhdlBlinkSimulationTemplate>();

        // The commented-out code block is left as is, as it was commented in the original.
        // If this functionality is desired, it would need further dependencies and
        // potentially a different registration approach (e.g., in a separate module
        // initializer or through an Autofac.Module if you're mixing DI containers).
        // if using CommunityToolkit.Mvvm.Input, you'd need to add that using here.
        // containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x,l) =>
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
    }

    // Instance method for path validation
    private bool IsRustHdlPathValid(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        // The PackageAutoSetting defines RelativePath = Path.Combine("vhdl_ls-x86_64-pc-windows-msvc", "bin", "vhdl_ls.exe")
        // So 'path' should already contain the full path to the executable.
        return File.Exists(path);
    }
}