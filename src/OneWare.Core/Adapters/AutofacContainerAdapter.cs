using Autofac;
using OneWare.Core.Adapters;

public class AutofacContainerAdapter : IContainerAdapter
{
    private IContainer _container ;
    private readonly ContainerBuilder _builder;

    public AutofacContainerAdapter()
    {
        _builder = new ContainerBuilder();
    }

    public AutofacContainerAdapter(IContainer container)
    {
        _container = container;
    }

    public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
    {
        var registration = _builder.RegisterType(implementationType).As(serviceType);
        if (!string.IsNullOrEmpty(name)) registration.Named(name, serviceType);
        if (isSingleton) registration.SingleInstance();
    }

    public void RegisterInstance(Type serviceType, object instance, string name = null)
    {
        var registration = _builder.RegisterInstance(instance).As(serviceType);
        if (!string.IsNullOrEmpty(name)) registration.Named(name, serviceType);
    }

    public void BuildContainer()
    {
        if (_container == null)
        {
            _container = _builder.Build();
        }
    }

    public object Resolve(Type serviceType, string name = null)
    {
        if (!string.IsNullOrEmpty(name))
            return _container.ResolveNamed(name, serviceType);

        return _container.Resolve(serviceType);
    }

    public bool IsRegistered(Type serviceType, string name = null)
    {
        if (!string.IsNullOrEmpty(name))
            return _container.IsRegisteredWithName(name, serviceType);

        return _container.IsRegistered(serviceType);
    }
}
