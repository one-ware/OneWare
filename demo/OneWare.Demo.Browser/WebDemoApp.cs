using OneWare.Core.ModuleLogic;
using Prism.Modularity;

namespace OneWare.Demo.Browser;

public class WebDemoApp : DemoApp
{
    protected override IModuleCatalog CreateModuleCatalog()
    {
        return new AggregateModuleCatalog();
    }

    public override string GetDefaultLayoutName => "Web";
}