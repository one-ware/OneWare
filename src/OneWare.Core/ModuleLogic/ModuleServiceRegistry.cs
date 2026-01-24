using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Core.ModuleLogic;

public sealed class ModuleServiceRegistry
{
    private readonly List<ServiceDescriptor> _descriptors = new();
    private readonly Dictionary<ServiceDescriptor, object?> _singletonCache = new();

    public void AddDescriptors(IEnumerable<ServiceDescriptor> descriptors)
    {
        _descriptors.AddRange(descriptors);
    }

    public bool IsRegistered(Type serviceType)
    {
        return _descriptors.Any(x => x.ServiceType == serviceType);
    }

    public object? GetService(Type serviceType, IServiceProvider provider)
    {
        var descriptor = _descriptors.LastOrDefault(x => x.ServiceType == serviceType);
        if (descriptor == null)
            return null;

        return CreateService(descriptor, provider);
    }

    public IEnumerable<object?> GetServices(Type serviceType, IServiceProvider provider)
    {
        foreach (var descriptor in _descriptors.Where(x => x.ServiceType == serviceType))
        {
            yield return CreateService(descriptor, provider);
        }
    }

    private object? CreateService(ServiceDescriptor descriptor, IServiceProvider provider)
    {
        if (descriptor.ImplementationInstance != null)
            return descriptor.ImplementationInstance;

        if (descriptor.Lifetime == ServiceLifetime.Singleton &&
            _singletonCache.TryGetValue(descriptor, out var cached))
            return cached;

        object? created = descriptor switch
        {
            { ImplementationFactory: not null } => descriptor.ImplementationFactory(provider),
            { ImplementationType: not null } => ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType),
            _ => null
        };

        if (descriptor.Lifetime == ServiceLifetime.Singleton)
            _singletonCache[descriptor] = created;

        return created;
    }
}

