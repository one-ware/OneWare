using DynamicData.Binding;
using OneWare.SDK.NativeTools;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.SDK.LanguageService;

public abstract class LanguageServiceLspAutoDownload : LanguageServiceLsp
{
    private readonly Func<Task> _installTask;
    protected LanguageServiceLspAutoDownload(IObservable<string> executablePath, Func<Task> install, string name, string? workspace) 
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
            await _installTask.Invoke();
        }
        await base.ActivateAsync();
    }
}