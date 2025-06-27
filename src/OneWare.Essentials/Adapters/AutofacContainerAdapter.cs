// ---- File: AutofacContainerAdapter.cs ----

using System;
using System.Reflection; // Keep this
using Autofac;
using OneWare.Essentials.Adapters;

namespace OneWare.Essentials.Adapters
{
    public class AutofacContainerAdapter : IContainerAdapter
    {
        private IContainer _container;
        private readonly ContainerBuilder _builder = new ContainerBuilder();
        private bool _isBuilt = false;

        // ADDED: New method to allow direct configuration of the internal ContainerBuilder
        public void ConfigureBuilder(Action<ContainerBuilder> configureAction)
        {
            if (_isBuilt) throw new InvalidOperationException("Cannot configure builder after container is built.");
            configureAction(_builder);
        }

        public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
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

        public void RegisterInstance(Type serviceType, object instance, string name = null)
        {
            var registration = _builder.RegisterInstance(instance).As(serviceType);

            if (!string.IsNullOrEmpty(name))
            {
                registration.Named(name, serviceType);
            }
        }

        public object Resolve(Type serviceType, string name = null)
        {
            EnsureBuilt();

            if (string.IsNullOrEmpty(name))
            {
                return _container.Resolve(serviceType);
            }
            else
            {
                return _container.ResolveNamed(name, serviceType);
            }
        }

        public T Resolve<T>(string name = null)
        {
            EnsureBuilt();

            if (string.IsNullOrEmpty(name))
            {
                return _container.Resolve<T>();
            }
            else
            {
                return _container.ResolveNamed<T>(name);
            }
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
            EnsureBuilt();

            if (string.IsNullOrEmpty(name))
            {
                return _container.IsRegistered(serviceType);
            }
            else
            {
                return _container.IsRegisteredWithName(name, serviceType);
            }
        }

        public void Build()
        {
            if (_isBuilt)
            {
                return;
            }

            _container = _builder.Build();
            _isBuilt = true;
        }

        private void EnsureBuilt()
        {
            if (!_isBuilt)
            {
                Build();
            }
        }

        // Helper method to register all types in an assembly that implement a specific interface
        public void RegisterAssemblyTypes(Assembly assembly)
        {
            _builder.RegisterAssemblyTypes(assembly)
                .AsImplementedInterfaces();
        }
    }
}