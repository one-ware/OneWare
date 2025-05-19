using Autofac;
using OneWare.Essentials.Services;

namespace OneWare.Json;

public class JsonModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Optionally register services here
        builder.RegisterType<JsonInitializer>()
               .AsSelf()
               .SingleInstance(); // Singleton is usually good for init
    }
}
