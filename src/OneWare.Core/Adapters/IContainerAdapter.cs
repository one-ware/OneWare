namespace OneWare.Core.Adapters
{
    public interface IContainerAdapter
    {
        void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false);
        void RegisterInstance(Type serviceType, object instance, string name = null);
        object Resolve(Type serviceType, string name = null);
        bool IsRegistered(Type serviceType, string name = null);
    }

}
