using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.LanguageService;

public abstract class LanguageServiceLspAutoDownload : LanguageServiceLsp
{
    private readonly Package _package;
    private readonly IPackageService _packageService;
    private bool _enableAutoDownload;

    protected LanguageServiceLspAutoDownload(IObservable<string> executablePath, Package package, string name,
        string? workspace, IPackageService packageService, IObservable<bool> enableAutoDownload,
        IChildProcessService childProcessService, PlatformHelper platformHelper, ILogger<LanguageServiceLsp> logger,
        IErrorService errorService, IDockService dockService, ILogger<LanguageServiceBase> baseLogger,
        IProjectExplorerService projectExplorerService)
        : base(name, childProcessService, workspace, platformHelper, logger, errorService, dockService, baseLogger, projectExplorerService)
    {
        _package = package;
        _packageService = packageService;

        enableAutoDownload.Subscribe(x => { _enableAutoDownload = x; });
        executablePath.Subscribe(x =>
        {
            ExecutablePath = x;
            if (File.Exists(ExecutablePath)) _ = ActivateAsync();
        });
    }

    public override async Task ActivateAsync()
    {
        if (!File.Exists(ExecutablePath) && _enableAutoDownload) await _packageService.InstallAsync(_package);
        await base.ActivateAsync();
    }
}