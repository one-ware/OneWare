using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OneWare.Core.Adapters
{
    public class TransitionContainerProxy : IContainerExtension
    {
        private readonly IContainerExtension _initialContainer;
        private IContainerAdapter _targetContainer;
        private readonly Queue<ContainerOperation> _operationQueue = new Queue<ContainerOperation>();
        private bool _isReleased = false;

        public IScopedProvider CurrentScope => throw new NotImplementedException();

        public TransitionContainerProxy(IContainerExtension initialContainer)
        {
            _initialContainer = initialContainer;
        }

        public void SetTargetContainer(IContainerAdapter targetContainer)
        {
            if (_isReleased) throw new InvalidOperationException("Target container already released");
            _targetContainer = targetContainer;
        }

        public void ReleaseToTarget()
        {
            if (_targetContainer == null) throw new InvalidOperationException("Target container not set");
            if (_isReleased) return;

            // Replay all operations on target container
            while (_operationQueue.Count > 0)
            {
                var operation = _operationQueue.Dequeue();
                operation.Execute(_targetContainer);
            }

            _isReleased = true;
        }

        public void FinalizeExtension() => _initialContainer.FinalizeExtension();

        public bool IsRegistered(Type type) =>
            _isReleased ? _targetContainer.IsRegistered(type) : _initialContainer.IsRegistered(type);

        public bool IsRegistered(Type type, string name) =>
            _isReleased ? _targetContainer.IsRegistered(type, name) : _initialContainer.IsRegistered(type, name);

        public IContainerRegistry Register(Type from, Type to) => Register(from, to, null);

        public IContainerRegistry Register(Type from, Type to, string name)
        {
            _initialContainer.Register(from, to, name);

            if (!_isReleased)
            {
                _operationQueue.Enqueue(new RegisterOperation(from, to, name, false));
            }
            return this;
        }

        public IContainerRegistry Register(Type type, Func<object> factoryMethod)
        {
            _initialContainer.Register(type, factoryMethod);

            if (!_isReleased)
            {
                _operationQueue.Enqueue(new RegisterFactoryOperation(type, factoryMethod, false));
            }
            return this;
        }

        public IContainerRegistry Register(Type type, Func<IContainerProvider, object> factoryMethod)
        {
            _initialContainer.Register(type, factoryMethod);

            if (!_isReleased)
            {
                // Wrap the factory method to adapt to IContainerAdapter
                Func<object> adapterFactory = () => factoryMethod(this);
                _operationQueue.Enqueue(new RegisterFactoryOperation(type, adapterFactory, false));
            }
            return this;
        }

        public IContainerRegistry RegisterSingleton(Type from, Type to) => RegisterSingleton(from, to, null);

        public IContainerRegistry RegisterSingleton(Type from, Type to, string name)
        {
            _initialContainer.RegisterSingleton(from, to, name);

            if (!_isReleased)
            {
                _operationQueue.Enqueue(new RegisterOperation(from, to, name, true));
            }
            return this;
        }

        public IContainerRegistry RegisterSingleton(Type type, Func<object> factoryMethod)
        {
            _initialContainer.RegisterSingleton(type, factoryMethod);

            if (!_isReleased)
            {
                _operationQueue.Enqueue(new RegisterFactoryOperation(type, factoryMethod, true));
            }
            return this;
        }

        public IContainerRegistry RegisterSingleton(Type type, Func<IContainerProvider, object> factoryMethod)
        {
            _initialContainer.RegisterSingleton(type, factoryMethod);

            if (!_isReleased)
            {
                // Wrap the factory method to adapt to IContainerAdapter
                Func<object> adapterFactory = () => factoryMethod(this);
                _operationQueue.Enqueue(new RegisterFactoryOperation(type, adapterFactory, true));
            }
            return this;
        }

        public IContainerRegistry RegisterInstance(Type type, object instance)
        {
            _initialContainer.RegisterInstance(type, instance);

            if (!_isReleased)
            {
                _operationQueue.Enqueue(new RegisterInstanceOperation(type, instance, null));
            }
            return this;
        }

        public IContainerRegistry RegisterInstance(Type type, object instance, string name)
        {
            _initialContainer.RegisterInstance(type, instance, name);

            if (!_isReleased)
            {
                _operationQueue.Enqueue(new RegisterInstanceOperation(type, instance, name));
            }
            return this;
        }

        public object Resolve(Type type) =>
            _isReleased ? _targetContainer.Resolve(type) : _initialContainer.Resolve(type);

        public object Resolve(Type type, string name) =>
            _isReleased ? _targetContainer.Resolve(type, name) : _initialContainer.Resolve(type, name);

        public object Resolve(Type type, params (Type Type, object Instance)[] parameters)
        {
            if (_isReleased)
            {
                // Simplified implementation - extend as needed
                return _targetContainer.Resolve(type);
            }
            return _initialContainer.Resolve(type, parameters);
        }

        public object Resolve(Type type, string name, params (Type Type, object Instance)[] parameters)
        {
            if (_isReleased)
            {
                // For Autofac, resolve using parameters
                if (parameters.Length == 0)
                    return _targetContainer.Resolve(type, name);

                var paramList = parameters.Select(p =>
                    new Autofac.NamedParameter(p.Type.Name, p.Instance) as Autofac.Core.Parameter
                ).ToList();

                return _initialContainer.Resolve(type, name, parameters);
            }

            return _initialContainer.Resolve(type, name, parameters);
        }

        public IScopedProvider CreateScope()
        {
            if (_isReleased)
            {
                // Create Autofac scope
                //var autofacScope = ((AutofacContainerAdapter)_targetContainer).CreateScope();
                //return new AutofacScopedProvider(autofacScope);
            }

            return _initialContainer.CreateScope();
        }

        public IContainerRegistry RegisterManySingleton(Type type, params Type[] serviceTypes)
        {
            throw new NotImplementedException();
        }

        public IContainerRegistry RegisterMany(Type type, params Type[] serviceTypes)
        {
            throw new NotImplementedException();
        }

        public IContainerRegistry RegisterScoped(Type from, Type to)
        {
            throw new NotImplementedException();
        }

        public IContainerRegistry RegisterScoped(Type type, Func<object> factoryMethod)
        {
            throw new NotImplementedException();
        }

        public IContainerRegistry RegisterScoped(Type type, Func<IContainerProvider, object> factoryMethod)
        {
            throw new NotImplementedException();
        }

        private abstract class ContainerOperation
        {
            public abstract void Execute(IContainerAdapter container);
        }

        private class RegisterOperation : ContainerOperation
        {
            private readonly Type _serviceType;
            private readonly Type _implementationType;
            private readonly string _name;
            private readonly bool _isSingleton;

            public RegisterOperation(Type serviceType, Type implementationType, string name, bool isSingleton)
            {
                _serviceType = serviceType;
                _implementationType = implementationType;
                _name = name;
                _isSingleton = isSingleton;
            }

            public override void Execute(IContainerAdapter container)
            {
                container.Register(_serviceType, _implementationType, _name, _isSingleton);
            }
        }

        private class RegisterFactoryOperation : ContainerOperation
        {
            private readonly Type _serviceType;
            private readonly Func<object> _factory;
            private readonly bool _isSingleton;

            public RegisterFactoryOperation(Type serviceType, Func<object> factory, bool isSingleton)
            {
                _serviceType = serviceType;
                _factory = factory;
                _isSingleton = isSingleton;
            }

            public override void Execute(IContainerAdapter container)
            {
                // For simplicity, treat factory registrations as instance registrations
                if (_isSingleton)
                {
                    container.RegisterInstance(_serviceType, _factory());
                }
                else
                {
                    container.Register(_serviceType, _factory().GetType(), null, false);
                }
            }
        }

        private class RegisterInstanceOperation : ContainerOperation
        {
            private readonly Type _serviceType;
            private readonly object _instance;
            private readonly string _name;

            public RegisterInstanceOperation(Type serviceType, object instance, string name)
            {
                _serviceType = serviceType;
                _instance = instance;
                _name = name;
            }

            public override void Execute(IContainerAdapter container)
            {
                container.RegisterInstance(_serviceType, _instance, _name);
            }
        }
    }
}
