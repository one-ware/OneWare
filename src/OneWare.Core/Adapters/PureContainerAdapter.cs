using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWare.Core.Adapters
{
    public class PureContainerAdapter : IContainerAdapter
    {
        private readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();
        private readonly Dictionary<(Type, string), Func<object>> _namedFactories = new Dictionary<(Type, string), Func<object>>();
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private readonly Dictionary<(Type, string), object> _namedSingletons = new Dictionary<(Type, string), object>();
        private readonly object _lock = new object();

        public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
        {
            if (isSingleton)
            {
                if (string.IsNullOrEmpty(name))
                    _singletons[serviceType] = Activator.CreateInstance(implementationType);
                else
                    _namedSingletons[(serviceType, name)] = Activator.CreateInstance(implementationType);
            }
            else
            {
                if (string.IsNullOrEmpty(name))
                    _factories[serviceType] = () => Activator.CreateInstance(implementationType);
                else
                    _namedFactories[(serviceType, name)] = () => Activator.CreateInstance(implementationType);
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

            throw new InvalidOperationException($"Service not registered: {serviceType.Name}");
        }

        public bool IsRegistered(Type serviceType, string name = null)
        {
            if (string.IsNullOrEmpty(name))
                return _singletons.ContainsKey(serviceType) || _factories.ContainsKey(serviceType);

            var key = (serviceType, name);
            return _namedSingletons.ContainsKey(key) || _namedFactories.ContainsKey(key);
        }
    }
}
