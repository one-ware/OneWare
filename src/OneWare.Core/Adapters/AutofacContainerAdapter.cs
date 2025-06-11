using System;
using Autofac;

namespace OneWare.Core.Adapters
{
    public class AutofacContainerAdapter : IContainerAdapter
    {
        private readonly ContainerBuilder _builder = new();
        private IContainer? _container;

        /// <summary>
        /// Builds the Autofac container once all registrations are done.
        /// </summary>
        public void BuildContainer()
        {
            if (_container == null)
            {
                _container = _builder.Build();
            }
        }

        /// <summary>
        /// Registers a service with its implementation type.
        /// Supports optional named registration and singleton lifetime.
        /// </summary>
        public void Register(Type serviceType, Type implementationType, string? name = null, bool isSingleton = false)
        {
            var registration = _builder.RegisterType(implementationType).As(serviceType);

            if (!string.IsNullOrEmpty(name))
            {
                registration.Named(name, serviceType);
            }

            if (isSingleton)
            {
                registration.SingleInstance();
            }
        }

        /// <summary>
        /// Registers a specific instance as a service.
        /// Supports optional named registration.
        /// </summary>
        public void RegisterInstance(Type serviceType, object instance, string? name = null)
        {
            var registration = _builder.RegisterInstance(instance).As(serviceType);

            if (!string.IsNullOrEmpty(name))
            {
                registration.Named(name, serviceType);
            }
        }

        /// <summary>
        /// Resolves a registered service by type and optional name.
        /// Throws if container is not yet built or service is not registered.
        /// </summary>
        public object Resolve(Type serviceType, string? name = null)
        {
            if (_container == null)
                throw new InvalidOperationException("Container is not built. Call BuildContainer() first.");

            return string.IsNullOrEmpty(name)
                ? _container.Resolve(serviceType)
                : _container.ResolveNamed(name, serviceType);
        }

        /// <summary>
        /// Checks if a service is registered by type and optional name.
        /// Returns false if container is not yet built.
        /// </summary>
        public bool IsRegistered(Type serviceType, string? name = null)
        {
            if (_container == null) return false;

            return string.IsNullOrEmpty(name)
                ? _container.IsRegistered(serviceType)
                : _container.IsRegisteredWithName(name, serviceType);
        }

        // Optional: Generic helpers for convenience

        /// <summary>
        /// Generic registration for services and implementations.
        /// </summary>
        public void Register<TService, TImplementation>(string? name = null, bool isSingleton = false)
            where TImplementation : TService
        {
            Register(typeof(TService), typeof(TImplementation), name, isSingleton);
        }

        /// <summary>
        /// Generic resolve helper.
        /// </summary>
        public TService Resolve<TService>(string? name = null)
        {
            return (TService)Resolve(typeof(TService), name);
        }
    }
}
