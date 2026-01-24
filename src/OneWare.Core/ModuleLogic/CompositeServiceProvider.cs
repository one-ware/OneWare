using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Core.ModuleLogic;

public sealed class CompositeServiceProvider : IServiceProvider, IServiceProviderIsService
{
    private readonly IServiceProvider _rootProvider;
    private readonly ModuleServiceRegistry _moduleRegistry;

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
        {
            return BuildEnumerable(serviceType);
        }

        return _moduleRegistry.GetService(serviceType, this) ?? _rootProvider.GetService(serviceType);
    }

    public bool IsService(Type serviceType)
    {
        if (_moduleRegistry.IsRegistered(serviceType))
            return true;

        if (_rootProvider is IServiceProviderIsService isService)
            return isService.IsService(serviceType);

        return _rootProvider.GetService(serviceType) != null;
    }

    private object BuildEnumerable(Type serviceType)
    {
        var elementType = serviceType.GetGenericArguments()[0];
        var items = new List<object?>();

        foreach (var item in _rootProvider.GetServices(elementType))
        {
            items.Add(item);
        }

        foreach (var item in _moduleRegistry.GetServices(elementType, this))
        {
            items.Add(item);
        }

        var array = Array.CreateInstance(elementType, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            array.SetValue(items[i], i);
        }

        return array;
    }
}
