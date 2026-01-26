using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Python;

public class PythonModule : OneWareModuleBase
{
    public const string LspName = "pylsp";
    public const string LspPathSetting = "PythonModule_PylspPath";

    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<ISettingsService>().RegisterSetting("Languages", "Python", LspPathSetting,
            new FilePathSetting("Pylsp Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path for Pylsp executable"
            });

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);

        serviceProvider.Resolve<ILanguageManager>()
            .RegisterService(typeof(LanguageServicePython), false, ".py");
    }
}
