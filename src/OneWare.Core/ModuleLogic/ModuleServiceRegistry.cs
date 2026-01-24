using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Core.ModuleLogic;

public sealed class ModuleServiceRegistry
{
    private readonly List<ServiceDescriptor> _descriptors = new();
    private readonly Dictionary<ServiceDescriptor, object?> _singletonCache = new();
    private readonly HashSet<Type> _registeredTypes = new();

    public void AddDescriptors(IEnumerable<ServiceDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            _descriptors.Add(descriptor);
            _registeredTypes.Add(descriptor.ServiceType);
        }
    }

    public void AddServiceTypes(IEnumerable<ServiceDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            _registeredTypes.Add(descriptor.ServiceType);
        }
    }

    public bool IsRegistered(Type serviceType)
    {
        if (_registeredTypes.Contains(serviceType))
            return true;

        if (serviceType.IsGenericType)
            return _registeredTypes.Contains(serviceType.GetGenericTypeDefinition());

        return false;
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
