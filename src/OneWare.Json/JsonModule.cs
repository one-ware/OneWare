using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;

namespace OneWare.Json;

public class JsonModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<ILanguageManager>()
            .RegisterStandaloneTypeAssistance(typeof(TypeAssistanceJson), ".json");
    }
}

