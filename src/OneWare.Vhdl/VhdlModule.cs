using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.Vhdl.Parsing;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Vhdl;

public class VhdlModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IErrorService>().RegisterErrorSource("RustHDL");
        containerProvider.Resolve<ILanguageManager>().RegisterTextMateLanguage("vhdl", "avares://OneWare.Vhdl/Assets/vhdl.tmLanguage.json", ".vhd", ".vhdl");
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceVhdl),true, ".vhd", ".vhdl");
        
        containerProvider.Resolve<NodeProviderService>().RegisterNodeProvider(new VhdlNodeProvider(), ".vhd", ".vhdl");
        
        containerProvider.Resolve<IProjectExplorerService>().RegisterContextMenu(x =>
        {
            if (x.Count == 1 && x.First() is IProjectFile { Extension: ".vhd" or ".vhdl" })
            {
                return new[]
                {
                    new MenuItemModel("Test")
                    {
                        Header = "Test"
                    }
                };
            }
            return null;
        });
    }
}