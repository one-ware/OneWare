using System;
using Prism.Ioc;

namespace OneWare.Core.Adapters
{
    public class PrismContainerAdapter : IContainerAdapter
    {
        private readonly IContainerRegistry _registry;
        private readonly IContainerProvider _provider;

        public PrismContainerAdapter(IContainerRegistry registry, IContainerProvider provider)
        {
            _registry = registry;
            _provider = provider;
        }

        public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
        {
            if (isSingleton)
            {
                if (string.IsNullOrEmpty(name))
                    _registry.RegisterSingleton(serviceType, implementationType);
                else
                    _registry.RegisterSingleton(serviceType, implementationType, name);
            }
            else
            {
                if (string.IsNullOrEmpty(name))
                    _registry.Register(serviceType, implementationType);
                else
                    _registry.Register(serviceType, implementationType, name);
            }
        }

        public void RegisterInstance(Type serviceType, object instance, string name = null)
        {
            if (string.IsNullOrEmpty(name))
                _registry.RegisterInstance(serviceType, instance);
            else
                _registry.RegisterInstance(serviceType, instance, name);
        }

        public object Resolve(Type serviceType, string name = null)
        {
            return string.IsNullOrEmpty(name)
                ? _provider.Resolve(serviceType)
                : _provider.Resolve(serviceType, name);
        }

        public T Resolve<T>(string name = null)
        {
            return string.IsNullOrEmpty(name)
                ? _provider.Resolve<T>()
                : _provider.Resolve<T>(name);
        }

        public void Register<TService, TImplementation>(string name = null, bool isSingleton = false)
            where TImplementation : TService
        {
            Register(typeof(TService), typeof(TImplementation), name, isSingleton);
        }

        public void RegisterInstance<TService>(TService instance, string name = null)
        {
            RegisterInstance(typeof(TService), instance, name);
        }

        public bool IsRegistered(Type serviceType, string name = null)
        {
            return string.IsNullOrEmpty(name)
                ? _registry.IsRegistered(serviceType)
                : _registry.IsRegistered(serviceType, name);
        }

        public void Build()
        {
            // No-op for Prism, but required by interface
        }
    }
}
