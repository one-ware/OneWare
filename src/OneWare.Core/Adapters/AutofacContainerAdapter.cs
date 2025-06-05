using Autofac;

namespace OneWare.Core.Adapters
{
    public class AutofacContainerAdapter : IContainerAdapter
    {
        private readonly ContainerBuilder _builder;
        private IContainer _container;

        public AutofacContainerAdapter(ContainerBuilder builder)
        {
            _builder = builder;
        }

        public void BuildContainer()
        {
            if (_container != null) return;
            _container = _builder.Build();
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
            if (_container == null) throw new InvalidOperationException("Container not built");

            return string.IsNullOrEmpty(name)
                ? _container.Resolve(serviceType)
                : _container.ResolveNamed(name, serviceType);
        }

        public bool IsRegistered(Type serviceType, string name = null)
        {
            if (_container == null) return false;

            return string.IsNullOrEmpty(name)
                ? _container.IsRegistered(serviceType)
                : _container.IsRegisteredWithName(name, serviceType);
        }
    }
}
