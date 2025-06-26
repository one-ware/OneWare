using System;
using System.Reflection; // Add this for Assembly type

namespace OneWare.Core.Adapters
{
    public interface IContainerAdapter
    {
        // Registers a type mapping with the container.
        void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false);

        // Registers an instance with the container.
        void RegisterInstance(Type serviceType, object instance, string name = null);

        // Resolves an instance of the specified type from the container.
        object Resolve(Type serviceType, string name = null);

        // Checks if a type is registered with the container.
        bool IsRegistered(Type serviceType, string name = null);

        // Generic version of Resolve to directly return an instance of the specified type.
        T Resolve<T>(string name = null);

        // Generic version of Register to map a service type to an implementation type.
        void Register<TService, TImplementation>(string name = null, bool isSingleton = false)
            where TImplementation : TService;

        // Generic version of RegisterInstance to register an instance of a type.
        void RegisterInstance<TService>(TService instance, string name = null);

        // Builds or finalizes the container setup.
        void Build();

        // Registers all types in an assembly that implement a specific interface.
        void RegisterAssemblyTypes(Assembly assembly);
    }
}
