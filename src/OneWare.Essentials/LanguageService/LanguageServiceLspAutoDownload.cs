using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.LanguageService;

public abstract class LanguageServiceLspAutoDownload : LanguageServiceLsp
{
    private readonly IPackageService _packageService;
    private readonly Package _package;
    private bool _enableAutoDownload = false;
    
    protected LanguageServiceLspAutoDownload(IObservable<string> executablePath, Package package, string name, string? workspace, IPackageService packageService, IObservable<bool> enableAutoDownload) 
        : base(name, workspace)
    {
        _package = package;
        _packageService = packageService;

        enableAutoDownload.Subscribe(x =>
        {
            _enableAutoDownload = x;
        });
        executablePath.Subscribe(x =>
        {
            ExecutablePath = x;
            if(File.Exists(ExecutablePath))
            {
                _ = ActivateAsync();
            }
        });
    }
    
    public override async Task ActivateAsync()
    {
        if (!File.Exists(ExecutablePath) && _enableAutoDownload)
        {
            await _packageService.InstallAsync(_package);
        }
        await base.ActivateAsync();
    }
}