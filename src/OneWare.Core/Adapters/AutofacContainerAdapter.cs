using Autofac;
using System;

namespace OneWare.Core.Adapters
{
    public class AutofacContainerAdapter : IContainerAdapter
    {
        private IContainer _container;
        private readonly ContainerBuilder _builder = new ContainerBuilder();
        private bool _isBuilt = false;

        public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
        {
            var registration = _builder.RegisterType(implementationType).As(serviceType);
            if (!string.IsNullOrEmpty(name))
                registration.Named(name, serviceType);
            if (isSingleton)
                registration.SingleInstance();
        }

        public void RegisterInstance(Type serviceType, object instance, string name = null)
        {
            var registration = _builder.RegisterInstance(instance).As(serviceType);
            if (!string.IsNullOrEmpty(name))
                registration.Named(name, serviceType);
        }

        public object Resolve(Type serviceType, string name = null)
        {
            EnsureBuilt();

            return string.IsNullOrEmpty(name)
                ? _container.Resolve(serviceType)
                : _container.ResolveNamed(name, serviceType);
        }

        public T Resolve<T>(string name = null)
        {
            EnsureBuilt();

            return string.IsNullOrEmpty(name)
                ? _container.Resolve<T>()
                : _container.ResolveNamed<T>(name);
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

            return string.IsNullOrEmpty(name)
                ? _container.IsRegistered(serviceType)
                : _container.IsRegisteredWithName(name, serviceType);
        }

        public void Build()
        {
            if (_isBuilt) return;

            _container = _builder.Build();
            _isBuilt = true;
        }

        private void EnsureBuilt()
        {
            if (!_isBuilt)
                Build();
        }
    }
}
