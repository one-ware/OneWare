using DynamicData.Binding;
using OneWare.SDK.NativeTools;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.SDK.LanguageService;

public abstract class LanguageServiceLspAutoDownload : LanguageServiceLsp
{
    private readonly Func<Task<bool>> _installTask;
    protected LanguageServiceLspAutoDownload(IObservable<string> executablePath, Func<Task<bool>> install, string name, string? workspace) 
        : base(name, workspace)
    {
        _installTask = install;
        
        executablePath.Subscribe(x =>
        {
            ExecutablePath = x;
        });
    }
    
    public override async Task ActivateAsync()
    {
        if (!File.Exists(ExecutablePath))
        {
            if(!await _installTask.Invoke()) return;
        }
        await base.ActivateAsync();
    }
}