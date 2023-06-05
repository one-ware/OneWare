using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Cpp;

public class CppModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<ILanguageManager>().RegisterHighlighting("avares://OneWare.Cpp/Assets/CPP-Mode.xshd", ".cpp", ".h", ".c", ".hpp");
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceCpp),false, ".cpp", ".h", ".c", ".hpp");
    }
}