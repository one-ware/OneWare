using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;

namespace OneWare.Core.ModuleLogic;

/// <summary>
///     This Service Provider provides both integrated and plugin services
/// </summary>
public sealed class CompositeServiceProvider : ICompositeServiceProvider
{
    private readonly ModuleServiceRegistry _moduleRegistry;
    private readonly IServiceProvider _rootProvider;

    public CompositeServiceProvider(IServiceProvider rootProvider, ModuleServiceRegistry moduleRegistry)
    {
        _rootProvider = rootProvider;
        _moduleRegistry = moduleRegistry;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider))
            return this;

        if (serviceType.IsGenericType &&
            serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return BuildEnumerable(serviceType);

        return _moduleRegistry.GetService(serviceType, this) ?? _rootProvider.GetService(serviceType);
    }

    public bool IsService(Type serviceType)
    {
        if (serviceType.IsGenericType &&
            serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var elementType = serviceType.GetGenericArguments()[0];
            return _moduleRegistry.IsRegistered(elementType);
        }

        return _moduleRegistry.IsRegistered(serviceType);
    }

    private object BuildEnumerable(Type serviceType)
    {
        var elementType = serviceType.GetGenericArguments()[0];
        var items = new List<object?>();

        foreach (var item in _rootProvider.GetServices(elementType)) items.Add(item);

        foreach (var item in _moduleRegistry.GetServices(elementType, this)) items.Add(item);

        var array = Array.CreateInstance(elementType, items.Count);
        for (var i = 0; i < items.Count; i++) array.SetValue(items[i], i);

        return array;
    }
}