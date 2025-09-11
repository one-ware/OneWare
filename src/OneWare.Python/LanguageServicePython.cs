using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Python;

public class LanguageServicePython : LanguageServiceLsp
{
    public LanguageServicePython(ISettingsService settingsService)
        : base(PythonModule.LspName, null)
    {
        settingsService.GetSettingObservable<string>(PythonModule.LspPathSetting).Subscribe(x =>
        {
            ExecutablePath = x;
        });
    }

    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistancePython(editor, this);
    }
}