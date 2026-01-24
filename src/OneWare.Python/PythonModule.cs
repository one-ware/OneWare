using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
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
        serviceProvider.Resolve<ISettingsService>().RegisterTitledFilePath("Languages", "Python", LspPathSetting,
            "Pylsp Path", "Path for Pylsp executable", "", null,
            serviceProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath, PlatformHelper.ExeFile);

        serviceProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);

        serviceProvider.Resolve<ILanguageManager>()
            .RegisterService(typeof(LanguageServicePython), false, ".py");
    }
}

