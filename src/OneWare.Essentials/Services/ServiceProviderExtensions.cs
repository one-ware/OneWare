using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Essentials.Services;

public static class ServiceProviderExtensions
{
    public static T Resolve<T>(this IServiceProvider? provider)
    {
        return (T)provider.Resolve(typeof(T));
    }

    public static object Resolve(this IServiceProvider? provider, Type type)
    {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));
        var service = provider.GetService(type);
        return service ?? ActivatorUtilities.CreateInstance(provider, type);
    }

    public static T Resolve<T>(this IServiceProvider? provider, params (Type type, object value)[] parameters)
    {
        return (T)provider.Resolve(typeof(T), parameters);
    }

    public static object Resolve(this IServiceProvider? provider, Type type,
        params (Type type, object value)[] parameters)
    {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));
        if (parameters.Length == 0)
            return provider.Resolve(type);

        var args = parameters.Select(x => x.value).ToArray();
        return ActivatorUtilities.CreateInstance(provider, type, args);
    }

    public static bool IsRegistered<T>(this IServiceProvider? provider)
    {
        return provider.IsRegistered(typeof(T));
    }

    public static bool IsRegistered(this IServiceProvider? provider, Type type)
    {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));
        if (provider is IServiceProviderIsService isService)
            return isService.IsService(type);

        return provider.GetService(type) != null;
    }

    public static IServiceProvider GetContainer(this IServiceProvider? provider)
    {
        return provider ?? throw new ArgumentNullException(nameof(provider));
    }
}