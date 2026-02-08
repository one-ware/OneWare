using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.CSharp;

public class CSharpModule : OneWareModuleBase
{
    public const string LspName = "csharp-ls";
    public const string LspPathSetting = "CSharpModule_LSPath";

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Languages", "CSharp", LspPathSetting,
            new FilePathSetting("CSharp LS Path", "csharp-ls", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for CSharp LS Path",
            });

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);

        serviceProvider.Resolve<ILanguageManager>()
            .RegisterService(typeof(LanguageServiceCSharp), true, ".cs");
    }
}