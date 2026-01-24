using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Services;

public static class ContainerLocator
{
    public static IServiceProvider? Container { get; private set; }

    public static IServiceProvider Current =>
        Container ?? throw new InvalidOperationException("Container has not been initialized.");

    public static void SetContainer(IServiceProvider provider)
    {
        Container = provider;
    }
}

