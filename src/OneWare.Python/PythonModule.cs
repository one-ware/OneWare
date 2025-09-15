using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Python;

public class PythonModule : IModule
{
    public const string LspName = "pylsp";
    public const string LspPathSetting = "PythonModule_PylspPath";
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<ISettingsService>().RegisterTitledFilePath("Languages", "Python", LspPathSetting,
            "Pylsp Path", "Path for Pylsp executable", "", null,
            containerProvider.Resolve<IPaths>().NativeToolsDirectory, PlatformHelper.ExistsOnPath, PlatformHelper.ExeFile);

        containerProvider.Resolve<IErrorService>().RegisterErrorSource(LspName);

        containerProvider.Resolve<ILanguageManager>()
            .RegisterService(typeof(LanguageServicePython), false, ".py");
    }
}