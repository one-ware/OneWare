using System;
using System.Collections.Generic;
using System.Reflection;

namespace OneWare.Essentials.Adapters
{
    public class PureContainerAdapter : IContainerAdapter
    {
        private readonly Dictionary<Type, Func<object>> _factories = new();
        private readonly Dictionary<(Type, string), Func<object>> _namedFactories = new();
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly Dictionary<(Type, string), object> _namedSingletons = new();
        private readonly object _lock = new();

        public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
        {
            if (isSingleton)
            {
                var instance = Activator.CreateInstance(implementationType);
                if (string.IsNullOrEmpty(name))
                    _singletons[serviceType] = instance;
                else
                    _namedSingletons[(serviceType, name)] = instance;
            }
            else
            {
                Func<object> factory = () => Activator.CreateInstance(implementationType);
                if (string.IsNullOrEmpty(name))
                    _factories[serviceType] = factory;
                else
                    _namedFactories[(serviceType, name)] = factory;
            }
        }

        public void RegisterInstance(Type serviceType, object instance, string name = null)
        {
            if (string.IsNullOrEmpty(name))
                _singletons[serviceType] = instance;
            else
                _namedSingletons[(serviceType, name)] = instance;
        }

        public object Resolve(Type serviceType, string name = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (_singletons.TryGetValue(serviceType, out var singleton))
                    return singleton;
                if (_factories.TryGetValue(serviceType, out var factory))
                    return factory();
            }
            else
            {
                var key = (serviceType, name);
                if (_namedSingletons.TryGetValue(key, out var namedSingleton))
                    return namedSingleton;
                if (_namedFactories.TryGetValue(key, out var namedFactory))
                    return namedFactory();
            }

            throw new InvalidOperationException($"Service not registered: {serviceType.FullName}");
        }

        public T Resolve<T>(string name = null)
        {
            return (T)Resolve(typeof(T), name);
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
            if (string.IsNullOrEmpty(name))
                return _singletons.ContainsKey(serviceType) || _factories.ContainsKey(serviceType);

            var key = (serviceType, name);
            return _namedSingletons.ContainsKey(key) || _namedFactories.ContainsKey(key);
        }

        public void Build()
        {
            // No-op for this simple container
        }

        public void RegisterAssemblyTypes(Assembly assembly)
        {
            throw new NotImplementedException();
        }
    }
}
